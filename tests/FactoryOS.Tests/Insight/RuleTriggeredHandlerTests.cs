using FactoryOS.Agents.Insight;
using FactoryOS.Agents.Insight.Application;
using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Tests.Insight;

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

    private sealed record Harness(RuleTriggeredHandler Handler, RecordingEventBus Bus, FakeLlmGateway Gateway);

    private static Harness Build(string reply = "OEE fell below target; inspect changeover and micro-stops on press-1.")
    {
        var bus = new RecordingEventBus();
        var gateway = new FakeLlmGateway(reply, model: "reasoning-x");
        var engine = new InsightEngine(bus, gateway, new InMemoryProcessedEventLog(), new InsightAgentOptions());
        return new Harness(new RuleTriggeredHandler(engine), bus, gateway);
    }

    private static RuleTriggered Fired(Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        RuleId = "oee-degraded",
        Metric = "Oee",
        MeterId = "press-1",
        Value = 0.53m,
        Operator = "LessThan",
        Threshold = 0.6m,
        Action = "RaiseMaintenanceAlert",
        TriggeredAt = DateTimeOffset.UnixEpoch,
        SourceEventId = Guid.NewGuid(),
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task A_fired_rule_yields_an_insight_naming_the_rule_and_metric()
    {
        var h = Build();
        var evt = Fired();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var insight = Assert.Single(h.Bus.Published.OfType<InsightGenerated>());
        Assert.Equal(nameof(RuleTriggered), insight.TriggerType);
        Assert.Equal("reasoning-x", insight.Model);
        Assert.Equal(evt.EventId, insight.SourceEventId);
        Assert.Equal(DateTimeOffset.UnixEpoch, insight.GeneratedAt);
        Assert.Contains("oee-degraded", insight.Subject, StringComparison.Ordinal);
        Assert.Contains("Oee", insight.Subject, StringComparison.Ordinal);
        Assert.Contains("press-1", insight.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_prompt_carries_the_fired_rule_details()
    {
        var h = Build();
        var evt = Fired();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var request = Assert.Single(h.Gateway.Requests);
        var user = Assert.Single(request.Messages, m => m.Role == ChatRole.User);
        Assert.Contains("RuleTriggered", user.Content, StringComparison.Ordinal);
        Assert.Contains("RaiseMaintenanceAlert", user.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redelivery_generates_the_insight_once()
    {
        var h = Build();
        var evt = Fired();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<InsightGenerated>());
    }
}
