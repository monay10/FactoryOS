using System.Collections.Concurrent;
using FactoryOS.Agents.Insight;
using FactoryOS.Ai.Gateway;
using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;
using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Safety;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The AI path proven over the real bus: a severe safety incident becomes a Safety stand-down, and the Insight
/// agent — reaching a language model only through the (stubbed) LLM Gateway — turns it into an
/// <see cref="InsightGenerated"/> fact back on the bus. AI output re-enters as just another event, and neither
/// plugin references the other. `SafetyIncidentReported → SafetyStandDownTriggered → InsightGenerated`.
/// </summary>
public sealed class SafetyToInsightChainTests
{
    private sealed class StubLlmGateway : ILlmGateway
    {
        public Task<Result<ChatCompletionResponse>> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(new ChatCompletionResponse
            {
                Model = "stub-model",
                Content = "Halt line, inspect for chemical exposure source, then review PPE compliance.",
                FinishReason = "stop",
            }));
    }

    private sealed class CaptureSink
    {
        public ConcurrentBag<IIntegrationEvent> Events { get; } = [];
    }

    private sealed class CapturingHandler<TEvent> : IEventHandler<TEvent>
        where TEvent : IIntegrationEvent
    {
        private readonly CaptureSink _sink;

        public CapturingHandler(CaptureSink sink) => _sink = sink;

        public Task HandleAsync(TEvent integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _sink.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_severe_incident_yields_an_ai_insight_on_the_bus()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton<ILlmGateway, StubLlmGateway>();
        new SafetyPlugin().ConfigureServices(services);
        new InsightAgentPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<InsightGenerated>, CapturingHandler<InsightGenerated>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new SafetyIncidentReported
        {
            Tenant = "acme",
            SiteId = "site-1",
            Severity = 5,
            Category = "Chemical",
            OccurredAt = DateTimeOffset.UnixEpoch,
        });

        var insight = Assert.Single(sink.Events.OfType<InsightGenerated>());
        Assert.Equal(nameof(SafetyStandDownTriggered), insight.TriggerType);
        Assert.Equal("stub-model", insight.Model);
        Assert.Contains("PPE", insight.Insight, StringComparison.Ordinal);
        Assert.Contains("site-1", insight.Subject, StringComparison.Ordinal);
    }
}
