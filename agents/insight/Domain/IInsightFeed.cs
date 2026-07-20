namespace FactoryOS.Agents.Insight.Domain;

/// <summary>
/// The read model over the agent's own output: a per-tenant, bounded, newest-first feed of generated insights.
/// It is fed purely by consuming <c>InsightGenerated</c> from the bus (never by an in-process call into the
/// reasoning path) and read over the gateway, closing the loop so AI output becomes a queryable, tenant-scoped
/// projection. Recording is idempotent by event id, honoring at-least-once delivery.
/// </summary>
public interface IInsightFeed
{
    /// <summary>Records an insight for a tenant, deduplicating by <see cref="InsightRecord.EventId"/>.</summary>
    /// <param name="tenant">The tenant the insight belongs to.</param>
    /// <param name="record">The insight to record.</param>
    /// <returns><see langword="true"/> if newly recorded; <see langword="false"/> if already seen.</returns>
    bool TryRecord(string tenant, InsightRecord record);

    /// <summary>Returns a tenant's most recent insights, newest first, capped at <paramref name="max"/>.</summary>
    /// <param name="tenant">The tenant to read.</param>
    /// <param name="max">The maximum number of insights to return; clamped to at least one.</param>
    /// <returns>The recent insights, newest first (empty for an unknown tenant).</returns>
    IReadOnlyList<InsightRecord> Recent(string tenant, int max);

    /// <summary>Summarizes a tenant's feed: the total kept and a per-trigger-type tally.</summary>
    /// <param name="tenant">The tenant to summarize.</param>
    /// <returns>The summary (zeroed for an unknown tenant).</returns>
    InsightFeedSummary Summarize(string tenant);
}

/// <summary>A tenant's insight-feed headline for an at-a-glance strip.</summary>
/// <param name="Tenant">The tenant summarized.</param>
/// <param name="Total">How many insights are currently kept for the tenant.</param>
/// <param name="ByTrigger">Per-trigger-type counts, ordered by count descending then trigger type.</param>
public sealed record InsightFeedSummary(
    string Tenant,
    int Total,
    IReadOnlyList<InsightTriggerTally> ByTrigger);

/// <summary>How many kept insights respond to one trigger type.</summary>
/// <param name="TriggerType">The trigger event type (for example <c>RuleTriggered</c>).</param>
/// <param name="Count">How many kept insights respond to it.</param>
public sealed record InsightTriggerTally(string TriggerType, int Count);
