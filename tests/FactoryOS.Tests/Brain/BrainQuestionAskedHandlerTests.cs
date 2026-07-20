using FactoryOS.Agents.Brain;
using FactoryOS.Agents.Brain.Application;
using FactoryOS.Ai.Brain;
using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;
using FactoryOS.Domain.Results;

namespace FactoryOS.Tests.Brain;

public sealed class BrainQuestionAskedHandlerTests
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

    private sealed class FakeBrain : ICompanyBrain
    {
        public BrainQuestion? LastQuestion { get; private set; }

        public Result<BrainAnswer> Next { get; set; } = Result.Success(new BrainAnswer
        {
            Answer = "Lubricate the pump bearings monthly.",
            Model = "fast-upstream",
            Citations = [new BrainCitation { Source = "pump-manual", ChunkId = "pump-manual#0", Score = 0.9 }],
        });

        public Task<Result<BrainAnswer>> AskAsync(BrainQuestion question, CancellationToken cancellationToken)
        {
            LastQuestion = question;
            return Task.FromResult(Next);
        }
    }

    private sealed record Harness(BrainQuestionAskedHandler Handler, RecordingEventBus Bus, FakeBrain Brain);

    private static Harness Build(BrainQueryAgentOptions? options = null)
    {
        var bus = new RecordingEventBus();
        var brain = new FakeBrain();
        var handler = new BrainQuestionAskedHandler(bus, brain, new InMemoryProcessedEventLog(), options ?? new BrainQueryAgentOptions());
        return new Harness(handler, bus, brain);
    }

    private static BrainQuestionAsked Asked(Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        Question = "how often to lubricate the pump?",
        AskedBy = "user:ali",
        AskedAt = DateTimeOffset.UnixEpoch,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task An_answer_is_generated_and_announced_with_the_configured_models()
    {
        var h = Build(new BrainQueryAgentOptions { ChatModel = "reasoning", EmbeddingModel = "embed-3", TopK = 6 });
        var evt = Asked();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        // The agent's configuration drove the question posed to the Brain.
        Assert.Equal("reasoning", h.Brain.LastQuestion!.ChatModel);
        Assert.Equal("embed-3", h.Brain.LastQuestion.EmbeddingModel);
        Assert.Equal(6, h.Brain.LastQuestion.TopK);
        Assert.Equal("acme", h.Brain.LastQuestion.Tenant);

        var answered = Assert.Single(h.Bus.Published.OfType<BrainAnswered>());
        Assert.Equal("Lubricate the pump bearings monthly.", answered.Answer);
        Assert.Equal("fast-upstream", answered.Model);
        Assert.Equal(evt.AskedAt, answered.AnsweredAt);
        Assert.Equal(evt.EventId, answered.SourceEventId);
        Assert.Equal("pump-manual", Assert.Single(answered.Citations).Source);
    }

    [Fact]
    public async Task A_brain_failure_throws_so_the_bus_retries()
    {
        var h = Build();
        h.Brain.Next = Result.Failure<BrainAnswer>(Error.Failure("Ai.Down", "gateway unreachable"));
        var evt = Asked();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None));

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_answers_only_once()
    {
        var h = Build();
        var evt = Asked();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<BrainAnswered>());
    }
}
