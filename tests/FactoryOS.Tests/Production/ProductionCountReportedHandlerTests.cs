using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Production.Application;
using FactoryOS.Plugins.Production.Domain;

namespace FactoryOS.Tests.Production;

public sealed class ProductionCountReportedHandlerTests
{
    private sealed class RecordingEventBus : IEventBus
    {
        public List<IIntegrationEvent> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent integrationEvent, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed record Harness(
        ProductionOrderReleasedHandler Release,
        ProductionCountReportedHandler Count,
        RecordingEventBus Bus,
        IProductionOrderStore Store);

    private static Harness Build(bool allowOverProduction = true)
    {
        var bus = new RecordingEventBus();
        var store = new InMemoryProductionOrderStore(allowOverProduction);
        var processed = new InMemoryProcessedEventLog();
        return new Harness(
            new ProductionOrderReleasedHandler(store),
            new ProductionCountReportedHandler(bus, store, processed),
            bus,
            store);
    }

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static ProductionOrderReleased Release(int target = 100) => new()
    {
        Tenant = "acme",
        OrderId = "PO-1",
        ProductId = "widget",
        TargetQuantity = target,
        ReleasedAt = DateTimeOffset.UnixEpoch,
    };

    private static ProductionCountReported Count(int produced) => new()
    {
        Tenant = "acme",
        OrderId = "PO-1",
        ProducedCount = produced,
        ReportedAt = DateTimeOffset.UnixEpoch.AddHours(1),
    };

    private static async Task Release(Harness h, ProductionOrderReleased release) =>
        await h.Release.HandleAsync(release, Context(release), CancellationToken.None);

    private static async Task Report(Harness h, ProductionCountReported count) =>
        await h.Count.HandleAsync(count, Context(count), CancellationToken.None);

    [Fact]
    public async Task Publishes_completed_when_accruals_reach_the_target()
    {
        var h = Build();
        await Release(h, Release(target: 100));

        await Report(h, Count(60));
        Assert.Empty(h.Bus.Published); // 60 < 100

        var completing = Count(40);
        await Report(h, completing);

        var done = Assert.Single(h.Bus.Published.OfType<ProductionOrderCompleted>());
        Assert.Equal("PO-1", done.OrderId);
        Assert.Equal("widget", done.ProductId);
        Assert.Equal(100, done.TargetQuantity);
        Assert.Equal(100, done.TotalProduced);
        Assert.Equal(completing.EventId, done.SourceEventId);
    }

    [Fact]
    public async Task Completion_is_emitted_only_once()
    {
        var h = Build();
        await Release(h, Release(target: 100));

        await Report(h, Count(100)); // completes
        await Report(h, Count(20));  // already completed

        Assert.Single(h.Bus.Published.OfType<ProductionOrderCompleted>());
    }

    [Fact]
    public async Task Counts_for_an_unreleased_order_are_ignored()
    {
        var h = Build();

        await Report(h, Count(100)); // never released

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_of_the_same_count_is_not_double_counted()
    {
        var h = Build();
        await Release(h, Release(target: 100));

        var count = Count(100);
        await Report(h, count);
        await Report(h, count); // same event id

        Assert.Single(h.Bus.Published.OfType<ProductionOrderCompleted>());
        Assert.Equal(100, Assert.Single(h.Store.ForTenant("acme")).TotalProduced); // not 200
    }

    [Fact]
    public async Task A_redelivered_release_does_not_reset_progress()
    {
        var h = Build();
        await Release(h, Release(target: 100));
        await Report(h, Count(60));

        await Release(h, Release(target: 100)); // redelivered release

        Assert.Equal(60, Assert.Single(h.Store.ForTenant("acme")).TotalProduced); // progress preserved
    }
}
