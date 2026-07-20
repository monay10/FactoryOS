namespace FactoryOS.Plugins.Brain.Domain;

/// <summary>
/// The per-tenant Brain answer log read-model: an append-only, newest-first record of the Company Brain's grounded
/// answers. Recording is idempotent by the producing event's id, so at-least-once delivery never doubles an entry.
/// Tenant-scoped by construction — no read or write crosses tenants.
/// </summary>
public interface IBrainAnswerLog
{
    /// <summary>Records an answer if its source event has not been recorded before.</summary>
    /// <param name="entry">The answer entry to append.</param>
    /// <returns><see langword="true"/> if newly recorded; <see langword="false"/> if it was a duplicate.</returns>
    bool Record(BrainAnswerEntry entry);

    /// <summary>Returns the most recent answers for a tenant, newest first.</summary>
    /// <param name="tenant">The tenant to read within.</param>
    /// <param name="max">The maximum number of answers to return.</param>
    /// <returns>Up to <paramref name="max"/> answers, newest first.</returns>
    IReadOnlyList<BrainAnswerEntry> Recent(string tenant, int max);

    /// <summary>Summarizes a tenant's answer log: the total kept and a per-model tally.</summary>
    /// <param name="tenant">The tenant to summarize.</param>
    /// <returns>The summary (zeroed for an unknown tenant).</returns>
    BrainAnswerLogSummary Summarize(string tenant);
}

/// <summary>A tenant's Brain answer-log headline for an at-a-glance strip.</summary>
/// <param name="Tenant">The tenant summarized.</param>
/// <param name="Total">How many answers are currently kept for the tenant.</param>
/// <param name="ByModel">Per-model counts, ordered by count descending then model name.</param>
public sealed record BrainAnswerLogSummary(
    string Tenant,
    int Total,
    IReadOnlyList<BrainModelTally> ByModel);

/// <summary>How many kept answers a single upstream model produced.</summary>
/// <param name="Model">The upstream chat model (for example <c>fast</c>).</param>
/// <param name="Count">How many kept answers it produced.</param>
public sealed record BrainModelTally(string Model, int Count);
