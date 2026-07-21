using System.Threading;

namespace FactoryOS.Plugins.Forms.Engine.Diagnostics;

/// <summary>An immutable snapshot of the forms engine's counters.</summary>
/// <param name="Opened">How many instances were opened.</param>
/// <param name="DraftsSaved">How many draft saves occurred.</param>
/// <param name="Submitted">How many submissions passed validation.</param>
/// <param name="ValidationFailures">How many submissions were blocked by validation.</param>
/// <param name="Approved">How many submissions were approved.</param>
/// <param name="Rejected">How many submissions were rejected.</param>
/// <param name="Cancelled">How many instances were cancelled.</param>
public sealed record FormMetricsSnapshot(
    long Opened,
    long DraftsSaved,
    long Submitted,
    long ValidationFailures,
    long Approved,
    long Rejected,
    long Cancelled);

/// <summary>
/// Thread-safe counters for the forms engine, incremented by the runtime as instances progress. A diagnostic
/// aid — reading the counters never affects behaviour.
/// </summary>
public sealed class FormMetrics
{
    private long _opened;
    private long _draftsSaved;
    private long _submitted;
    private long _validationFailures;
    private long _approved;
    private long _rejected;
    private long _cancelled;

    /// <summary>Records that an instance was opened.</summary>
    public void RecordOpened() => Interlocked.Increment(ref _opened);

    /// <summary>Records that a draft was saved.</summary>
    public void RecordDraftSaved() => Interlocked.Increment(ref _draftsSaved);

    /// <summary>Records that a submission passed validation.</summary>
    public void RecordSubmitted() => Interlocked.Increment(ref _submitted);

    /// <summary>Records that a submission was blocked by validation.</summary>
    public void RecordValidationFailure() => Interlocked.Increment(ref _validationFailures);

    /// <summary>Records that a submission was approved.</summary>
    public void RecordApproved() => Interlocked.Increment(ref _approved);

    /// <summary>Records that a submission was rejected.</summary>
    public void RecordRejected() => Interlocked.Increment(ref _rejected);

    /// <summary>Records that an instance was cancelled.</summary>
    public void RecordCancelled() => Interlocked.Increment(ref _cancelled);

    /// <summary>Reads the current counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public FormMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _opened),
        Interlocked.Read(ref _draftsSaved),
        Interlocked.Read(ref _submitted),
        Interlocked.Read(ref _validationFailures),
        Interlocked.Read(ref _approved),
        Interlocked.Read(ref _rejected),
        Interlocked.Read(ref _cancelled));
}
