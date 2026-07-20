using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Workflow;
using FactoryOS.Plugins.Workflow.Application;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Tests.Workflow;

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

    private sealed record Harness(RuleTriggeredHandler Handler, RecordingEventBus Bus);

    private static Harness Build(params WorkflowRule[] rules)
    {
        var bus = new RecordingEventBus();
        var ruleSet = new WorkflowRuleSet(new WorkflowOptions { Rules = rules });
        var engine = new WorkflowEngine(bus, ruleSet, new InMemoryProcessedEventLog());
        return new Harness(new RuleTriggeredHandler(engine), bus);
    }

    private static RuleTriggered Fired(Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        RuleId = "power-spike",
        Metric = "ActivePower",
        MeterId = "line-3",
        Value = 540m,
        Operator = "GreaterOrEqual",
        Threshold = 500m,
        Action = "NotifyEnergyDesk",
        TriggeredAt = DateTimeOffset.UnixEpoch,
        SourceEventId = Guid.NewGuid(),
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task A_fired_rule_requests_the_mapped_action_carrying_the_rule_in_the_subject()
    {
        var h = Build(new WorkflowRule { Trigger = "RuleTriggered", Action = "Notify", Priority = "High", Channel = "ops" });
        var evt = Fired();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var action = Assert.Single(h.Bus.Published.OfType<WorkflowActionRequested>());
        Assert.Equal("RuleTriggered", action.TriggerType);
        Assert.Equal("Notify", action.Action);
        Assert.Equal("ops", action.Channel);
        Assert.Equal("High", action.Priority);
        Assert.Equal("acme", action.Tenant);
        Assert.Contains("power-spike", action.Subject, StringComparison.Ordinal);
        Assert.Contains("NotifyEnergyDesk", action.Subject, StringComparison.Ordinal);
        Assert.Equal(evt.EventId, action.SourceEventId);
    }

    [Fact]
    public async Task No_rule_for_rule_triggers_means_no_action()
    {
        var h = Build(); // no rules configured
        var evt = Fired();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_requests_the_action_once()
    {
        var h = Build(new WorkflowRule { Trigger = "RuleTriggered", Action = "Notify" });
        var evt = Fired();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<WorkflowActionRequested>());
    }
}
