using System.Threading;

namespace FactoryOS.Plugins.Workflow.Audit.Diagnostics;

/// <summary>An immutable snapshot of the audit engine's counters.</summary>
/// <param name="Recorded">How many records were sealed into the chain.</param>
/// <param name="Filtered">How many entries were filtered out before recording.</param>
/// <param name="Archived">How many records were archived.</param>
/// <param name="Restored">How many records were restored from the archive.</param>
/// <param name="Expired">How many records outlived their retention.</param>
/// <param name="Exported">How many records were exported.</param>
/// <param name="Searches">How many searches were run.</param>
/// <param name="TamperDetections">How many chain verifications found a broken link.</param>
public sealed record AuditMetricsSnapshot(
    long Recorded,
    long Filtered,
    long Archived,
    long Restored,
    long Expired,
    long Exported,
    long Searches,
    long TamperDetections);

/// <summary>
/// Thread-safe counters for the audit engine, incremented by the runtime as records are sealed and maintained.
/// A diagnostic aid — reading the counters never affects behaviour.
/// </summary>
public sealed class AuditMetrics
{
    private long _recorded;
    private long _filtered;
    private long _archived;
    private long _restored;
    private long _expired;
    private long _exported;
    private long _searches;
    private long _tamperDetections;

    /// <summary>Records that a record was sealed into the chain.</summary>
    public void RecordRecorded() => Interlocked.Increment(ref _recorded);

    /// <summary>Records that an entry was filtered out before recording.</summary>
    public void RecordFiltered() => Interlocked.Increment(ref _filtered);

    /// <summary>Records that records were archived.</summary>
    /// <param name="count">How many.</param>
    public void RecordArchived(int count) => Interlocked.Add(ref _archived, count);

    /// <summary>Records that records were restored from the archive.</summary>
    /// <param name="count">How many.</param>
    public void RecordRestored(int count) => Interlocked.Add(ref _restored, count);

    /// <summary>Records that records outlived their retention.</summary>
    /// <param name="count">How many.</param>
    public void RecordExpired(int count) => Interlocked.Add(ref _expired, count);

    /// <summary>Records that records were exported.</summary>
    /// <param name="count">How many.</param>
    public void RecordExported(int count) => Interlocked.Add(ref _exported, count);

    /// <summary>Records that a search was run.</summary>
    public void RecordSearch() => Interlocked.Increment(ref _searches);

    /// <summary>Records that a chain verification found a broken link.</summary>
    public void RecordTamperDetection() => Interlocked.Increment(ref _tamperDetections);

    /// <summary>Reads the current counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public AuditMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _recorded),
        Interlocked.Read(ref _filtered),
        Interlocked.Read(ref _archived),
        Interlocked.Read(ref _restored),
        Interlocked.Read(ref _expired),
        Interlocked.Read(ref _exported),
        Interlocked.Read(ref _searches),
        Interlocked.Read(ref _tamperDetections));
}
