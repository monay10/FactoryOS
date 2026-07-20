using FactoryOS.Agents.Knowledge;
using FactoryOS.Ai.Gateway;
using FactoryOS.Ai.Knowledge;
using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Domain.Results;
using FactoryOS.Plugins.RuleEngine;
using FactoryOS.Plugins.RuleEngine.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The AI memory path proven over the real bus: a reading crosses a rule threshold, and the Knowledge Ingest agent
/// — reaching an embedding model only through the (fake) Embedding Gateway the indexer owns — writes the fired
/// rule as a retrievable, tenant-scoped knowledge chunk. The live event stream becomes Company Brain memory, and
/// neither the Rule Engine nor the agent references the other. `MeterReadingReceived → RuleTriggered → knowledge`.
/// </summary>
public sealed class KnowledgeIngestChainTests
{
    /// <summary>A deterministic embedding: axis 0 constant (never a zero vector), plus one axis per keyword.</summary>
    private sealed class KeywordEmbeddingGateway : IEmbeddingGateway
    {
        public Task<Result<EmbeddingResponse>> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken)
        {
            var vectors = request.Inputs.Select(Embed).ToList();
            return Task.FromResult(Result.Success(new EmbeddingResponse { Model = request.Model, Vectors = vectors }));
        }

        private static IReadOnlyList<float> Embed(string text) =>
        [
            1f,
            text.Contains("press-1", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
            text.Contains("Temperature", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
        ];
    }

    [Fact]
    public async Task A_fired_rule_becomes_retrievable_tenant_scoped_knowledge()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        // The knowledge stack the agent depends on, with a fake embedding gateway (no real model).
        services.AddSingleton<IEmbeddingGateway, KeywordEmbeddingGateway>();
        services.AddSingleton<IKnowledgeStore, InMemoryKnowledgeStore>();
        services.AddSingleton<IKnowledgeIndexer, KnowledgeIndexer>();

        services.AddSingleton(new RuleEngineOptions
        {
            Rules =
            [
                new RuleDefinition
                {
                    Id = "overtemp-press-1",
                    Metric = "Temperature",
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 85m,
                    Action = "RaiseMaintenanceAlert",
                },
            ],
        });

        new RuleEnginePlugin().ConfigureServices(services);
        new KnowledgeAgentPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var store = provider.GetRequiredService<IKnowledgeStore>();

        await bus.PublishAsync(new MeterReadingReceived
        {
            Reading = new MeterReading
            {
                Tenant = "acme",
                MeterId = "press-1",
                Metric = "Temperature",
                Value = 90m,
                Unit = "°C",
                ReadingAt = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
            },
        });

        // The narrative is now retrievable for the asking tenant …
        var hits = await store.SearchAsync("acme", [1f, 1f, 1f], 5, CancellationToken.None);
        Assert.True(hits.IsSuccess);
        var hit = Assert.Single(hits.Value);
        Assert.Contains("overtemp-press-1", hit.Chunk.Text, StringComparison.Ordinal);
        Assert.StartsWith("activity/rule/", hit.Chunk.Source, StringComparison.Ordinal);

        // … and unreachable for any other tenant.
        var otherTenant = await store.SearchAsync("globex", [1f, 1f, 1f], 5, CancellationToken.None);
        Assert.True(otherTenant.IsSuccess);
        Assert.Empty(otherTenant.Value);
    }
}
