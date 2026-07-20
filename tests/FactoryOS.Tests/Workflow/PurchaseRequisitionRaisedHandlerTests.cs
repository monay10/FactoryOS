using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Workflow;
using FactoryOS.Plugins.Workflow.Application;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Tests.Workflow;

public sealed class PurchaseRequisitionRaisedHandlerTests
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

    private sealed record Harness(PurchaseRequisitionRaisedHandler Handler, RecordingEventBus Bus);

    private static Harness Build(params WorkflowRule[] rules)
    {
        var bus = new RecordingEventBus();
        var ruleSet = new WorkflowRuleSet(new WorkflowOptions { Rules = rules });
        var engine = new WorkflowEngine(bus, ruleSet, new InMemoryProcessedEventLog());
        return new Harness(new PurchaseRequisitionRaisedHandler(engine), bus);
    }

    private static PurchaseRequisitionRaised Raised(Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Requisition = new PurchaseRequisition
        {
            Tenant = "acme",
            Number = "REQ-1001",
            Sku = "SKU-9",
            WarehouseId = "wh-main",
            RequestedQuantity = 120m,
            Status = "Draft",
        },
        Reason = "LowStock",
        SourceEventId = Guid.NewGuid(),
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task A_raised_requisition_requests_the_mapped_action_carrying_the_requisition_in_the_subject()
    {
        var h = Build(new WorkflowRule { Trigger = "PurchaseRequisitionRaised", Action = "Notify", Priority = "Normal", Channel = "procurement" });
        var evt = Raised();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var action = Assert.Single(h.Bus.Published.OfType<WorkflowActionRequested>());
        Assert.Equal("PurchaseRequisitionRaised", action.TriggerType);
        Assert.Equal("Notify", action.Action);
        Assert.Equal("procurement", action.Channel);
        Assert.Equal("acme", action.Tenant);
        Assert.Contains("REQ-1001", action.Subject, StringComparison.Ordinal);
        Assert.Contains("SKU-9", action.Subject, StringComparison.Ordinal);
        Assert.Contains("LowStock", action.Subject, StringComparison.Ordinal);
        Assert.Equal(evt.EventId, action.SourceEventId);
    }

    [Fact]
    public async Task No_rule_for_requisitions_means_no_action()
    {
        var h = Build(); // no rules configured
        var evt = Raised();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_requests_the_action_once()
    {
        var h = Build(new WorkflowRule { Trigger = "PurchaseRequisitionRaised", Action = "Notify" });
        var evt = Raised();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<WorkflowActionRequested>());
    }
}
