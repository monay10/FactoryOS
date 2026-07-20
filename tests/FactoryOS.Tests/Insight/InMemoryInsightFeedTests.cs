using FactoryOS.Agents.Insight.Domain;

namespace FactoryOS.Tests.Insight;

public sealed class InMemoryInsightFeedTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);

    private static InsightRecord Record(string trigger = "RuleTriggered", string subject = "s", Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), trigger, subject, "insight text", "model-x", At);

    [Fact]
    public void Recent_returns_insights_newest_first()
    {
        var feed = new InMemoryInsightFeed();
        var first = Record(subject: "first");
        var second = Record(subject: "second");

        feed.TryRecord("acme", first);
        feed.TryRecord("acme", second);

        var recent = feed.Recent("acme", 10);
        Assert.Equal("second", recent[0].Subject);
        Assert.Equal("first", recent[1].Subject);
    }

    [Fact]
    public void Recording_is_idempotent_by_event_id()
    {
        var feed = new InMemoryInsightFeed();
        var id = Guid.NewGuid();

        Assert.True(feed.TryRecord("acme", Record(id: id)));
        Assert.False(feed.TryRecord("acme", Record(id: id))); // same event id

        Assert.Single(feed.Recent("acme", 10));
    }

    [Fact]
    public void Recent_clamps_max_to_at_least_one()
    {
        var feed = new InMemoryInsightFeed();
        feed.TryRecord("acme", Record());
        feed.TryRecord("acme", Record());

        Assert.Single(feed.Recent("acme", 0));
    }

    [Fact]
    public void The_feed_is_bounded_and_evicts_the_oldest()
    {
        var feed = new InMemoryInsightFeed();
        var oldest = Record(subject: "oldest");
        feed.TryRecord("acme", oldest);
        for (var i = 0; i < InMemoryInsightFeed.Capacity; i++)
        {
            feed.TryRecord("acme", Record(subject: $"n-{i}"));
        }

        var recent = feed.Recent("acme", InMemoryInsightFeed.Capacity + 10);
        Assert.Equal(InMemoryInsightFeed.Capacity, recent.Count);
        Assert.DoesNotContain(recent, r => r.Subject == "oldest");
    }

    [Fact]
    public void Summarize_tallies_by_trigger_type_most_common_first()
    {
        var feed = new InMemoryInsightFeed();
        feed.TryRecord("acme", Record(trigger: "RuleTriggered"));
        feed.TryRecord("acme", Record(trigger: "RuleTriggered"));
        feed.TryRecord("acme", Record(trigger: "SafetyStandDownTriggered"));

        var summary = feed.Summarize("acme");

        Assert.Equal(3, summary.Total);
        Assert.Equal("RuleTriggered", summary.ByTrigger[0].TriggerType);
        Assert.Equal(2, summary.ByTrigger[0].Count);
        Assert.Equal("SafetyStandDownTriggered", summary.ByTrigger[1].TriggerType);
    }

    [Fact]
    public void Tenants_are_isolated()
    {
        var feed = new InMemoryInsightFeed();
        feed.TryRecord("acme", Record());

        Assert.Empty(feed.Recent("globex", 10));
        Assert.Equal(0, feed.Summarize("globex").Total);
    }
}
