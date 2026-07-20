using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Brain;
using FactoryOS.Plugins.Brain.Application;
using FactoryOS.Plugins.Brain.Domain;

namespace FactoryOS.Tests.Brain;

public sealed class BrainAnsweredHandlerTests
{
    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task A_grounded_answer_becomes_a_log_entry_with_its_citations()
    {
        var log = new InMemoryBrainAnswerLog(new BrainReadModelOptions());
        var handler = new BrainAnsweredHandler(log);
        var evt = new BrainAnswered
        {
            Tenant = "acme",
            Question = "Why did press-1 spike last night?",
            Answer = "An energy spike was detected on press-1, 32.5% above baseline.",
            Model = "fast",
            Citations = [new BrainCitation { Source = "activity/energy/abc", ChunkId = "activity/energy/abc#0", Score = 0.91 }],
            AnsweredAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(log.Recent("acme", 10));
        Assert.Equal("Why did press-1 spike last night?", entry.Question);
        Assert.Equal("fast", entry.Model);
        Assert.Equal(evt.SourceEventId, entry.SourceEventId);
        Assert.Equal("activity/energy/abc", Assert.Single(entry.Citations).Source);
    }

    [Fact]
    public async Task Redelivery_of_the_same_answer_is_a_no_op()
    {
        var log = new InMemoryBrainAnswerLog(new BrainReadModelOptions());
        var handler = new BrainAnsweredHandler(log);
        var evt = new BrainAnswered
        {
            Tenant = "acme",
            Question = "q",
            Answer = "a",
            Model = "fast",
            AnsweredAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Single(log.Recent("acme", 10));
    }
}
