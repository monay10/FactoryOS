using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Tests.Activity;

public sealed class InMemoryActivityFeedTests
{
    private static ActivityEntry Entry(string tenant, string headline, Guid? id = null, DateTimeOffset? at = null) =>
        new(tenant, "Rule", headline, at ?? DateTimeOffset.UnixEpoch, id ?? Guid.NewGuid());

    private static ActivityEntry Categorized(string tenant, string category, string headline) =>
        new(tenant, category, headline, DateTimeOffset.UnixEpoch, Guid.NewGuid());

    [Fact]
    public void Recorded_entries_come_back_newest_first()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());

        feed.Record(Entry("acme", "first"));
        feed.Record(Entry("acme", "second"));
        feed.Record(Entry("acme", "third"));

        var recent = feed.Recent("acme", 10);
        Assert.Equal(["third", "second", "first"], recent.Select(e => e.Headline));
    }

    [Fact]
    public void Recording_the_same_source_event_twice_is_a_no_op()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var id = Guid.NewGuid();

        Assert.True(feed.Record(Entry("acme", "once", id)));
        Assert.False(feed.Record(Entry("acme", "again", id)));

        Assert.Single(feed.Recent("acme", 10));
    }

    [Fact]
    public void The_feed_is_bounded_to_the_configured_capacity()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions { FeedCapacity = 2 });

        feed.Record(Entry("acme", "a"));
        feed.Record(Entry("acme", "b"));
        feed.Record(Entry("acme", "c"));

        var recent = feed.Recent("acme", 10);
        Assert.Equal(["c", "b"], recent.Select(e => e.Headline));
    }

    [Fact]
    public void Feeds_are_isolated_per_tenant()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());

        feed.Record(Entry("acme", "acme-1"));
        feed.Record(Entry("globex", "globex-1"));

        Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("globex-1", Assert.Single(feed.Recent("globex", 10)).Headline);
    }

    [Fact]
    public void An_unknown_tenant_reads_empty()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        Assert.Empty(feed.Recent("nobody", 10));
    }

    [Fact]
    public void A_category_filter_returns_only_matching_entries_newest_first()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());

        feed.Record(Categorized("acme", "Rule", "rule-1"));
        feed.Record(Categorized("acme", "Production", "prod-1"));
        feed.Record(Categorized("acme", "Rule", "rule-2"));
        feed.Record(Categorized("acme", "Production", "prod-2"));

        var production = feed.Recent("acme", 10, "Production");
        Assert.Equal(["prod-2", "prod-1"], production.Select(e => e.Headline));
    }

    [Fact]
    public void The_category_filter_is_case_insensitive()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());

        feed.Record(Categorized("acme", "Production", "prod-1"));

        Assert.Equal("prod-1", Assert.Single(feed.Recent("acme", 10, "production")).Headline);
    }

    [Fact]
    public void A_null_or_whitespace_category_returns_every_category()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());

        feed.Record(Categorized("acme", "Rule", "rule-1"));
        feed.Record(Categorized("acme", "Production", "prod-1"));

        Assert.Equal(2, feed.Recent("acme", 10, null).Count);
        Assert.Equal(2, feed.Recent("acme", 10, "   ").Count);
    }

    [Fact]
    public void The_category_filter_caps_at_max_matches()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());

        feed.Record(Categorized("acme", "Production", "prod-1"));
        feed.Record(Categorized("acme", "Rule", "rule-1"));
        feed.Record(Categorized("acme", "Production", "prod-2"));
        feed.Record(Categorized("acme", "Production", "prod-3"));

        var capped = feed.Recent("acme", 2, "Production");
        Assert.Equal(["prod-3", "prod-2"], capped.Select(e => e.Headline));
    }

    [Fact]
    public void The_summary_totals_and_tallies_by_category_count_descending()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());

        feed.Record(Categorized("acme", "Production", "prod-1"));
        feed.Record(Categorized("acme", "Rule", "rule-1"));
        feed.Record(Categorized("acme", "Production", "prod-2"));
        feed.Record(Categorized("acme", "Insight", "insight-1"));
        feed.Record(Categorized("acme", "Production", "prod-3"));

        var summary = feed.Summarize("acme");

        Assert.Equal("acme", summary.Tenant);
        Assert.Equal(5, summary.Total);
        Assert.Equal(
            [("Production", 3), ("Insight", 1), ("Rule", 1)],
            summary.ByCategory.Select(t => (t.Category, t.Count)));
    }

    [Fact]
    public void The_summary_is_tenant_scoped()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());

        feed.Record(Categorized("acme", "Rule", "rule-1"));
        feed.Record(Categorized("globex", "Safety", "safety-1"));

        Assert.Equal("Rule", Assert.Single(feed.Summarize("acme").ByCategory).Category);
    }

    [Fact]
    public void An_unknown_tenant_summarizes_to_zero()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());

        var summary = feed.Summarize("nobody");

        Assert.Equal(0, summary.Total);
        Assert.Empty(summary.ByCategory);
    }
}
