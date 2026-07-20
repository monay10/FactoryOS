using FactoryOS.Contracts.Ai;
using FactoryOS.Plugins.Brain;
using FactoryOS.Plugins.Brain.Domain;

namespace FactoryOS.Tests.Brain;

public sealed class InMemoryBrainAnswerLogTests
{
    private static readonly IReadOnlyList<BrainCitation> NoCitations = [];

    private static BrainAnswerEntry Entry(string tenant, string question, Guid? id = null, DateTimeOffset? at = null) =>
        new(tenant, question, $"answer to {question}", "fast", NoCitations, at ?? DateTimeOffset.UnixEpoch, id ?? Guid.NewGuid());

    private static BrainAnswerEntry Modelled(string tenant, string model) =>
        new(tenant, "q", "a", model, NoCitations, DateTimeOffset.UnixEpoch, Guid.NewGuid());

    [Fact]
    public void Recorded_answers_come_back_newest_first()
    {
        var log = new InMemoryBrainAnswerLog(new BrainReadModelOptions());

        log.Record(Entry("acme", "first"));
        log.Record(Entry("acme", "second"));
        log.Record(Entry("acme", "third"));

        Assert.Equal(["third", "second", "first"], log.Recent("acme", 10).Select(e => e.Question));
    }

    [Fact]
    public void Recording_the_same_source_event_twice_is_a_no_op()
    {
        var log = new InMemoryBrainAnswerLog(new BrainReadModelOptions());
        var id = Guid.NewGuid();

        Assert.True(log.Record(Entry("acme", "once", id)));
        Assert.False(log.Record(Entry("acme", "again", id)));

        Assert.Single(log.Recent("acme", 10));
    }

    [Fact]
    public void The_log_is_bounded_to_the_configured_capacity()
    {
        var log = new InMemoryBrainAnswerLog(new BrainReadModelOptions { LogCapacity = 2 });

        log.Record(Entry("acme", "a"));
        log.Record(Entry("acme", "b"));
        log.Record(Entry("acme", "c"));

        Assert.Equal(["c", "b"], log.Recent("acme", 10).Select(e => e.Question));
    }

    [Fact]
    public void Logs_are_isolated_per_tenant()
    {
        var log = new InMemoryBrainAnswerLog(new BrainReadModelOptions());

        log.Record(Entry("acme", "acme-q"));
        log.Record(Entry("globex", "globex-q"));

        Assert.Equal("acme-q", Assert.Single(log.Recent("acme", 10)).Question);
        Assert.Equal("globex-q", Assert.Single(log.Recent("globex", 10)).Question);
    }

    [Fact]
    public void An_unknown_tenant_reads_empty()
    {
        var log = new InMemoryBrainAnswerLog(new BrainReadModelOptions());
        Assert.Empty(log.Recent("nobody", 10));
    }

    [Fact]
    public void Recent_caps_at_max()
    {
        var log = new InMemoryBrainAnswerLog(new BrainReadModelOptions());

        log.Record(Entry("acme", "a"));
        log.Record(Entry("acme", "b"));
        log.Record(Entry("acme", "c"));

        Assert.Equal(["c", "b"], log.Recent("acme", 2).Select(e => e.Question));
    }

    [Fact]
    public void The_summary_totals_and_tallies_by_model_count_descending()
    {
        var log = new InMemoryBrainAnswerLog(new BrainReadModelOptions());

        log.Record(Modelled("acme", "fast"));
        log.Record(Modelled("acme", "reasoning"));
        log.Record(Modelled("acme", "fast"));
        log.Record(Modelled("acme", "fast"));

        var summary = log.Summarize("acme");

        Assert.Equal("acme", summary.Tenant);
        Assert.Equal(4, summary.Total);
        Assert.Equal(
            [("fast", 3), ("reasoning", 1)],
            summary.ByModel.Select(t => (t.Model, t.Count)));
    }

    [Fact]
    public void An_unknown_tenant_summarizes_to_zero()
    {
        var log = new InMemoryBrainAnswerLog(new BrainReadModelOptions());

        var summary = log.Summarize("nobody");

        Assert.Equal(0, summary.Total);
        Assert.Empty(summary.ByModel);
    }
}
