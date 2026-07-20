using System.Collections.Concurrent;
using FactoryOS.Agents.Brain;
using FactoryOS.Ai.Brain;
using FactoryOS.Ai.Gateway;
using FactoryOS.Ai.Knowledge;
using FactoryOS.Ai.Prompts;
using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;
using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Notification;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The conversational-AI loop extended to delivery over the real bus, zero inter-module references: a question
/// asked on the bus is answered by the Brain Query agent through a real Company Brain (RAG + stubbed LLM Gateway),
/// re-entering as <see cref="BrainAnswered"/>, which the Notification module routes to a transport and announces as
/// <see cref="NotificationDispatched"/>. The AI answer is delivered through the same door as any other
/// notification. `BrainQuestionAsked → BrainAnswered → NotificationDispatched`.
/// </summary>
public sealed class BrainAnswerToNotificationChainTests
{
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
    public async Task An_answered_question_is_dispatched_as_a_notification()
    {
        var embeddings = new KeywordEmbeddingGateway();
        var store = new InMemoryKnowledgeStore();
        var indexer = new KnowledgeIndexer(embeddings, store);
        await indexer.IngestAsync(
            new KnowledgeDocument { Tenant = "acme", Source = "pump-manual", Text = "The centrifugal pump needs monthly lubrication." },
            "embed", CancellationToken.None);

        var retriever = new KnowledgeRetriever(embeddings, store);
        var catalog = new InMemoryPromptCatalog();
        catalog.Register(BrainPrompts.Answer);
        var composer = new PromptComposer(catalog, new PromptRenderer());
        var brain = new CompanyBrain(retriever, composer, new StubLlmGateway());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton<ICompanyBrain>(brain);
        services.AddSingleton(new NotificationOptions
        {
            ChannelTransports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["assistant"] = "chat" },
        });

        new BrainQueryAgentPlugin().ConfigureServices(services);
        new NotificationPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<NotificationDispatched>, CapturingHandler<NotificationDispatched>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new BrainQuestionAsked
        {
            Tenant = "acme",
            Question = "how often does the pump need lubrication?",
            AskedBy = "user:ali",
            AskedAt = new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero),
        });

        var dispatched = Assert.Single(sink.Events.OfType<NotificationDispatched>());
        Assert.Equal("acme", dispatched.Tenant);
        Assert.Equal("assistant", dispatched.Channel);
        Assert.Equal("chat", dispatched.Transport);
        Assert.Contains("lubrication", dispatched.Subject, StringComparison.OrdinalIgnoreCase);
    }
}
