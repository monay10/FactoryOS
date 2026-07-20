using System.Collections.Concurrent;
using FactoryOS.Agents.Insight;
using FactoryOS.Ai.Gateway;
using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;
using FactoryOS.Domain.Results;
using FactoryOS.Plugins.RuleEngine;
using FactoryOS.Plugins.RuleEngine.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The AI-explains-automation path over the real bus, two plugins, zero inter-module references: a computed
/// <see cref="OeeCalculated"/> crosses a rule threshold (Rule Engine → <see cref="RuleTriggered"/>), and the
/// Insight agent — reaching a language model only through the (stubbed) LLM Gateway — turns the fired rule into
/// an <see cref="InsightGenerated"/> fact back on the bus. The digital worker explains what the Rule Engine only
/// detected; neither plugin references the other. `OeeCalculated → RuleTriggered → InsightGenerated`.
/// </summary>
public sealed class RuleToInsightChainTests
{
    private sealed class StubLlmGateway : ILlmGateway
    {
        public Task<Result<ChatCompletionResponse>> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(new ChatCompletionResponse
            {
                Model = "stub-model",
                Content = "OEE degradation likely from micro-stops; audit changeover and short stoppages on the press.",
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
    public async Task Degraded_oee_fires_a_rule_that_the_agent_explains()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton<ILlmGateway, StubLlmGateway>();

        services.AddSingleton(new RuleEngineOptions
        {
            Rules =
            [
                new RuleDefinition
                {
                    Id = "oee-degraded",
                    Metric = "Oee",
                    Operator = ComparisonOperator.LessThan,
                    Threshold = 0.6m,
                    Action = "RaiseMaintenanceAlert",
                },
            ],
        });

        new RuleEnginePlugin().ConfigureServices(services);
        new InsightAgentPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<InsightGenerated>, CapturingHandler<InsightGenerated>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new OeeCalculated
        {
            Tenant = "acme",
            MachineId = "press-1",
            PeriodStart = new DateTimeOffset(2026, 7, 20, 5, 0, 0, TimeSpan.Zero),
            PeriodEnd = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
            Availability = 0.9m,
            Performance = 0.7m,
            Quality = 0.85m,
            Oee = 0.53m,
            MeetsTarget = false,
        });

        var insight = Assert.Single(sink.Events.OfType<InsightGenerated>());
        Assert.Equal(nameof(RuleTriggered), insight.TriggerType);
        Assert.Equal("stub-model", insight.Model);
        Assert.Contains("changeover", insight.Insight, StringComparison.Ordinal);
        Assert.Contains("oee-degraded", insight.Subject, StringComparison.Ordinal);
        Assert.Contains("press-1", insight.Subject, StringComparison.Ordinal);
    }
}
