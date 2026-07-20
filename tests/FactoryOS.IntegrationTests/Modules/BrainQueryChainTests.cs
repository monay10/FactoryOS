using System.Collections.Concurrent;
using FactoryOS.Agents.Brain;
using FactoryOS.Ai.Brain;
using FactoryOS.Ai.Gateway;
using FactoryOS.Ai.Knowledge;
using FactoryOS.Ai.Prompts;
using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;
using FactoryOS.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The full conversational-AI loop over the real bus: a question asked on the bus
/// (<see cref="BrainQuestionAsked"/>) is answered by the Brain Query agent through a real Company Brain — RAG
/// retrieval over the tenant's indexed knowledge plus generation through a stubbed LLM Gateway — and the grounded,
/// cited answer re-enters as <see cref="BrainAnswered"/>. Asking and answering are decoupled events; the model is
/// reached only through the gateway. `BrainQuestionAsked → (retrieve + generate) → BrainAnswered`.
/// </summary>
public sealed class BrainQueryChainTests
{
    /// <summary>A deterministic keyword embedding, so retrieval works offline without a real model.</summary>
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
            text.Contains("pump", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
            text.Contains("boiler", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
        ];
    }

    private sealed class StubLlmGateway : ILlmGateway
    {
        public Task<Result<ChatCompletionResponse>> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(new ChatCompletionResponse
            {
                Model = "fast-upstream",
                Content = "The centrifugal pump needs monthly lubrication.",
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
    public async Task A_question_on_the_bus_is_answered_from_indexed_knowledge()
    {
        var embeddings = new KeywordEmbeddingGateway();
        var store = new InMemoryKnowledgeStore();

        // Seed the tenant's knowledge base (as the Knowledge Ingest agent would).
        var indexer = new KnowledgeIndexer(embeddings, store);
        await indexer.IngestAsync(
            new KnowledgeDocument { Tenant = "acme", Source = "pump-manual", Text = "The centrifugal pump needs monthly lubrication." },
            "embed", CancellationToken.None);

        // Build a real Company Brain over the seeded stack.
        var retriever = new KnowledgeRetriever(embeddings, store);
        var catalog = new InMemoryPromptCatalog();
        catalog.Register(BrainPrompts.Answer);
        var composer = new PromptComposer(catalog, new PromptRenderer());
        var brain = new CompanyBrain(retriever, composer, new StubLlmGateway());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton<ICompanyBrain>(brain);
        new BrainQueryAgentPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<BrainAnswered>, CapturingHandler<BrainAnswered>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new BrainQuestionAsked
        {
            Tenant = "acme",
            Question = "how often does the pump need lubrication?",
            AskedBy = "user:ali",
            AskedAt = new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero),
        });

        var answered = Assert.Single(sink.Events.OfType<BrainAnswered>());
        Assert.Equal("acme", answered.Tenant);
        Assert.Contains("lubrication", answered.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("fast-upstream", answered.Model);
        Assert.Equal("pump-manual", Assert.Single(answered.Citations).Source);
    }
}
