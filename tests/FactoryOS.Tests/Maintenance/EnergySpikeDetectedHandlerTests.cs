using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Maintenance;
using FactoryOS.Plugins.Maintenance.Application;
using FactoryOS.Plugins.Maintenance.Domain;

namespace FactoryOS.Tests.Maintenance;

public sealed class EnergySpikeDetectedHandlerTests
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

    private sealed record Harness(EnergySpikeDetectedHandler Handler, RecordingEventBus Bus, IWorkOrderStore Store);

    private static Harness Build()
    {
        var bus = new RecordingEventBus();
        var store = new InMemoryWorkOrderStore();
        return new Harness(new EnergySpikeDetectedHandler(bus, store, new MaintenanceOptions()), bus, store);
    }

    private static EnergySpikeDetected Spike(Guid eventId) => new()
    {
        EventId = eventId,
        Tenant = "acme",
        MeterId = "main-incomer",
        Metric = "ActivePower",
        Value = 250m,
        Baseline = 100m,
        DeltaPercent = 150m,
        Unit = "kWh",
        ReadingAt = DateTimeOffset.UnixEpoch,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task Raises_a_work_order_and_announces_it()
    {
        var h = Build();
        var spike = Spike(Guid.NewGuid());

        await h.Handler.HandleAsync(spike, Context(spike), CancellationToken.None);

        var created = Assert.Single(h.Bus.Published.OfType<WorkOrderCreated>());
        Assert.Equal("EnergySpike", created.Reason);
        Assert.Equal(spike.EventId, created.SourceEventId);
        Assert.Equal("main-incomer", created.WorkOrder.AssetCode);
        Assert.Single(h.Store.ForTenant("acme"));
    }

    [Fact]
    public async Task Redelivery_of_the_same_spike_does_not_create_a_second_work_order()
    {
        var h = Build();
        var spike = Spike(Guid.NewGuid());

        await h.Handler.HandleAsync(spike, Context(spike), CancellationToken.None);
        await h.Handler.HandleAsync(spike, Context(spike), CancellationToken.None); // at-least-once duplicate

        Assert.Single(h.Bus.Published.OfType<WorkOrderCreated>());
        Assert.Single(h.Store.ForTenant("acme"));
    }
}
