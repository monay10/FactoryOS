using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Oee;
using FactoryOS.Plugins.Oee.Application;
using FactoryOS.Plugins.Oee.Domain;

namespace FactoryOS.Tests.Oee;

public sealed class ProductionPeriodReportedHandlerTests
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

    private sealed record Harness(ProductionPeriodReportedHandler Handler, RecordingEventBus Bus, IOeeStore Store);

    private static Harness Build(decimal target = 0.85m)
    {
        var bus = new RecordingEventBus();
        var store = new InMemoryOeeStore();
        return new Harness(new ProductionPeriodReportedHandler(bus, store, new OeeOptions { TargetOee = target }), bus, store);
    }

    private static ProductionPeriodReported Period(decimal planned = 100m, decimal run = 90m, int total = 72, int good = 54) => new()
    {
        Tenant = "acme",
        MachineId = "press-1",
        PeriodStart = DateTimeOffset.UnixEpoch,
        PeriodEnd = DateTimeOffset.UnixEpoch.AddHours(8),
        PlannedTimeSeconds = planned,
        RunTimeSeconds = run,
        IdealCycleTimeSeconds = 1m,
        TotalCount = total,
        GoodCount = good,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task Computes_and_publishes_oee_below_target()
    {
        var h = Build(target: 0.85m);

        await h.Handler.HandleAsync(Period(), Context(Period()), CancellationToken.None);

        var calc = Assert.Single(h.Bus.Published.OfType<OeeCalculated>());
        Assert.Equal(0.9m, calc.Availability);
        Assert.Equal(0.8m, calc.Performance);
        Assert.Equal(0.75m, calc.Quality);
        Assert.Equal(0.54m, calc.Oee);
        Assert.False(calc.MeetsTarget); // 0.54 < 0.85
        Assert.Single(h.Store.ForTenant("acme"));
    }

    [Fact]
    public async Task Flags_meets_target_when_the_threshold_is_reached()
    {
        var h = Build(target: 0.5m);

        await h.Handler.HandleAsync(Period(), Context(Period()), CancellationToken.None);

        Assert.True(Assert.Single(h.Bus.Published.OfType<OeeCalculated>()).MeetsTarget); // 0.54 >= 0.5
    }

    [Fact]
    public async Task Skips_a_period_with_no_planned_time()
    {
        var h = Build();

        await h.Handler.HandleAsync(Period(planned: 0m), Context(Period()), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
        Assert.Empty(h.Store.ForTenant("acme"));
    }

    [Fact]
    public async Task Redelivery_of_the_same_period_is_ignored()
    {
        var h = Build();
        var period = Period();

        await h.Handler.HandleAsync(period, Context(period), CancellationToken.None);
        await h.Handler.HandleAsync(period, Context(period), CancellationToken.None); // same machine-period

        Assert.Single(h.Bus.Published.OfType<OeeCalculated>());
    }
}
