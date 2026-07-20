using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Workflow;
using FactoryOS.Plugins.Workflow.Application;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Tests.Workflow;

public sealed class WorkOrderCreatedHandlerTests
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

    private sealed record Harness(WorkOrderCreatedHandler Handler, RecordingEventBus Bus);

    private static Harness Build(params WorkflowRule[] rules)
    {
        var bus = new RecordingEventBus();
        var ruleSet = new WorkflowRuleSet(new WorkflowOptions { Rules = rules });
        var engine = new WorkflowEngine(bus, ruleSet, new InMemoryProcessedEventLog());
        return new Harness(new WorkOrderCreatedHandler(engine), bus);
    }

    private static WorkOrderCreated Created(Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Reason = "Rule:overtemp-press-1",
        WorkOrder = new WorkOrder
        {
            Tenant = "acme",
            Number = "WOR-ABCDEF123",
            Title = "Rule overtemp-press-1 fired on press-1",
            Status = "Open",
            AssetCode = "press-1",
            DueAt = DateTimeOffset.UnixEpoch.AddHours(24),
        },
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task A_created_work_order_requests_the_mapped_action()
    {
        var h = Build(new WorkflowRule { Trigger = "WorkOrderCreated", Action = "Notify", Priority = "High", Channel = "maintenance" });
        var evt = Created();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var action = Assert.Single(h.Bus.Published.OfType<WorkflowActionRequested>());
        Assert.Equal("WorkOrderCreated", action.TriggerType);
        Assert.Equal("Notify", action.Action);
        Assert.Equal("maintenance", action.Channel);
        Assert.Equal("acme", action.Tenant);
        Assert.Contains("WOR-ABCDEF123", action.Subject, StringComparison.Ordinal);
        Assert.Equal(evt.EventId, action.SourceEventId);
    }

    [Fact]
    public async Task No_rule_for_work_orders_means_no_action()
    {
        var h = Build(); // no rules
        var evt = Created();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_requests_the_action_once()
    {
        var h = Build(new WorkflowRule { Trigger = "WorkOrderCreated", Action = "Notify" });
        var evt = Created();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<WorkflowActionRequested>());
    }
}
