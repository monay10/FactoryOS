using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Workflow;
using FactoryOS.Plugins.Workflow.Application;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Tests.Workflow;

public sealed class WorkflowEngineTests
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

    private sealed record Harness(WorkflowEngine Engine, RecordingEventBus Bus);

    private static Harness Build(params WorkflowRule[] rules)
    {
        var bus = new RecordingEventBus();
        var ruleSet = new WorkflowRuleSet(new WorkflowOptions { Rules = rules });
        return new Harness(new WorkflowEngine(bus, ruleSet, new InMemoryProcessedEventLog()), bus);
    }

    private static WorkflowSignal Signal(Guid? id = null) =>
        new("acme", "QualityAlertRaised", "Quality defect rate 8.0% on line-1/widget", DateTimeOffset.UnixEpoch, id ?? Guid.NewGuid());

    [Fact]
    public async Task Requests_the_mapped_action_when_a_rule_matches()
    {
        var h = Build(new WorkflowRule { Trigger = "QualityAlertRaised", Action = "Notify", Priority = "High", Channel = "quality" });
        var signal = Signal();

        await h.Engine.ProcessAsync(signal, CancellationToken.None);

        var action = Assert.Single(h.Bus.Published.OfType<WorkflowActionRequested>());
        Assert.Equal("QualityAlertRaised", action.TriggerType);
        Assert.Equal("Notify", action.Action);
        Assert.Equal("High", action.Priority);
        Assert.Equal("quality", action.Channel);
        Assert.Equal(signal.SourceEventId, action.SourceEventId);
    }

    [Fact]
    public async Task Does_nothing_when_no_rule_matches()
    {
        var h = Build(); // no rules

        await h.Engine.ProcessAsync(Signal(), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Acts_on_a_signal_only_once()
    {
        var h = Build(new WorkflowRule { Trigger = "QualityAlertRaised", Action = "Notify" });
        var signal = Signal();

        await h.Engine.ProcessAsync(signal, CancellationToken.None);
        await h.Engine.ProcessAsync(signal, CancellationToken.None); // same source event id

        Assert.Single(h.Bus.Published.OfType<WorkflowActionRequested>());
    }
}
