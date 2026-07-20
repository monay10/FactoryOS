using FactoryOS.Agents.Insight;
using FactoryOS.Agents.Insight.Application;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Tests.Insight;

public sealed class InsightEngineTests
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

    private static InsightSignal Signal(Guid? id = null) =>
        new("acme", "SafetyStandDownTriggered", "Safety stand-down at site-1", DateTimeOffset.UnixEpoch, id ?? Guid.NewGuid());

    [Fact]
    public async Task Generates_and_publishes_an_insight_on_success()
    {
        var bus = new RecordingEventBus();
        var gateway = new FakeLlmGateway("Likely calibration drift; recalibrate press-1.", model: "gpt-x");
        var engine = new InsightEngine(bus, gateway, new InMemoryProcessedEventLog(), new InsightAgentOptions());
        var signal = Signal();

        await engine.GenerateAsync(signal, CancellationToken.None);

        var insight = Assert.Single(bus.Published.OfType<InsightGenerated>());
        Assert.Equal("SafetyStandDownTriggered", insight.TriggerType);
        Assert.Equal("Likely calibration drift; recalibrate press-1.", insight.Insight);
        Assert.Equal("gpt-x", insight.Model);
        Assert.Equal(signal.SourceEventId, insight.SourceEventId);
        Assert.Equal(DateTimeOffset.UnixEpoch, insight.GeneratedAt);
    }

    [Fact]
    public async Task Generates_an_insight_only_once_per_trigger()
    {
        var bus = new RecordingEventBus();
        var engine = new InsightEngine(bus, new FakeLlmGateway("insight"), new InMemoryProcessedEventLog(), new InsightAgentOptions());
        var signal = Signal();

        await engine.GenerateAsync(signal, CancellationToken.None);
        await engine.GenerateAsync(signal, CancellationToken.None); // same source event id

        Assert.Single(bus.Published.OfType<InsightGenerated>());
    }

    [Fact]
    public async Task A_gateway_failure_throws_and_publishes_nothing()
    {
        var bus = new RecordingEventBus();
        var engine = new InsightEngine(bus, FakeLlmGateway.Failing(), new InMemoryProcessedEventLog(), new InsightAgentOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.GenerateAsync(Signal(), CancellationToken.None));
        Assert.Empty(bus.Published);
    }
}
