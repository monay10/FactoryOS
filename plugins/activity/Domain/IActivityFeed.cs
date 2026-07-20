namespace FactoryOS.Plugins.Activity.Domain;

/// <summary>
/// The per-tenant activity feed read-model: an append-only, newest-first record of noteworthy
/// events. Recording is idempotent by the producing event's id, so at-least-once delivery never doubles an entry.
/// Tenant-scoped by construction — no read or write crosses tenants.
/// </summary>
public interface IActivityFeed
{
    /// <summary>Records an entry if its source event has not been recorded before.</summary>
    /// <param name="entry">The activity entry to append.</param>
    /// <returns><see langword="true"/> if newly recorded; <see langword="false"/> if it was a duplicate.</returns>
    bool Record(ActivityEntry entry);

    /// <summary>Returns the most recent entries for a tenant, newest first, optionally narrowed to one category.</summary>
    /// <param name="tenant">The tenant to read within.</param>
    /// <param name="max">The maximum number of entries to return.</param>
    /// <param name="category">An optional category to filter by (case-insensitive); <see langword="null"/> or
    /// whitespace returns entries of every category.</param>
    /// <returns>Up to <paramref name="max"/> entries matching the filter, newest first.</returns>
    IReadOnlyList<ActivityEntry> Recent(string tenant, int max, string? category = null);

    /// <summary>Summarizes a tenant's feed: the total kept and a per-category tally.</summary>
    /// <param name="tenant">The tenant to summarize.</param>
    /// <returns>The summary (zeroed for an unknown tenant).</returns>
    ActivityFeedSummary Summarize(string tenant);
}

/// <summary>A tenant's activity-feed headline for an at-a-glance strip.</summary>
/// <param name="Tenant">The tenant summarized.</param>
/// <param name="Total">How many entries are currently kept for the tenant.</param>
/// <param name="ByCategory">Per-category counts, ordered by count descending then category name.</param>
public sealed record ActivityFeedSummary(
    string Tenant,
    int Total,
    IReadOnlyList<ActivityCategoryTally> ByCategory);

/// <summary>How many kept entries fall in one category.</summary>
/// <param name="Category">The category (for example <c>Insight</c>).</param>
/// <param name="Count">How many kept entries fall in it.</param>
public sealed record ActivityCategoryTally(string Category, int Count);
