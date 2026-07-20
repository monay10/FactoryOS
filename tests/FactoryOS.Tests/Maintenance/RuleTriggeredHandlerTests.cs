using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Maintenance;
using FactoryOS.Plugins.Maintenance.Application;
using FactoryOS.Plugins.Maintenance.Domain;

namespace FactoryOS.Tests.Maintenance;

public sealed class RuleTriggeredHandlerTests
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

    private sealed record Harness(RuleTriggeredHandler Handler, RecordingEventBus Bus, IWorkOrderStore Store);

    private static Harness Build(MaintenanceOptions? options = null)
    {
        var bus = new RecordingEventBus();
        var store = new InMemoryWorkOrderStore();
        return new Harness(new RuleTriggeredHandler(bus, store, options ?? new MaintenanceOptions()), bus, store);
    }

    private static RuleTriggered Triggered(string action, Guid? eventId = null) => new()
    {
        EventId = eventId ?? Guid.NewGuid(),
        Tenant = "acme",
        RuleId = "overtemp-press-1",
        Metric = "Temperature",
        MeterId = "press-1",
        Value = 90m,
        Operator = "GreaterThan",
        Threshold = 85m,
        Action = action,
        TriggeredAt = DateTimeOffset.UnixEpoch,
        SourceEventId = Guid.NewGuid(),
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task A_maintenance_action_raises_a_work_order_and_announces_it()
    {
        var h = Build();
        var evt = Triggered("RaiseMaintenanceAlert");

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var created = Assert.Single(h.Bus.Published.OfType<WorkOrderCreated>());
        Assert.Equal("Rule:overtemp-press-1", created.Reason);
        Assert.Equal(evt.EventId, created.SourceEventId);
        Assert.Equal("press-1", created.WorkOrder.AssetCode);
        Assert.Equal("Open", created.WorkOrder.Status);
        Assert.Single(h.Store.ForTenant("acme"));
    }

    [Fact]
    public async Task Action_matching_is_case_insensitive()
    {
        var h = Build();
        var evt = Triggered("raisemaintenancealert");

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Single(h.Bus.Published.OfType<WorkOrderCreated>());
    }

    [Fact]
    public async Task An_action_the_module_does_not_own_is_ignored()
    {
        var h = Build();
        var evt = Triggered("NotifyEnergyDesk"); // not in RuleActions

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
        Assert.Empty(h.Store.ForTenant("acme"));
    }

    [Fact]
    public async Task Redelivery_of_the_same_trigger_does_not_create_a_second_work_order()
    {
        var h = Build();
        var evt = Triggered("RaiseMaintenanceAlert");

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // at-least-once duplicate

        Assert.Single(h.Bus.Published.OfType<WorkOrderCreated>());
        Assert.Single(h.Store.ForTenant("acme"));
    }

    [Fact]
    public async Task Two_rules_on_one_reading_raise_two_distinct_work_orders()
    {
        var h = Build();
        var a = Triggered("RaiseMaintenanceAlert");
        var b = Triggered("RaiseMaintenanceAlert"); // distinct trigger event id

        await h.Handler.HandleAsync(a, Context(a), CancellationToken.None);
        await h.Handler.HandleAsync(b, Context(b), CancellationToken.None);

        Assert.Equal(2, h.Bus.Published.OfType<WorkOrderCreated>().Count());
        Assert.Equal(2, h.Store.ForTenant("acme").Count);
    }
}
