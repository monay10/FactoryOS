using System.Threading;

namespace FactoryOS.Plugins.Workflow.Tasks.Diagnostics;

/// <summary>An immutable snapshot of the human task engine's counters.</summary>
/// <param name="Created">How many tasks were created.</param>
/// <param name="Completed">How many tasks were completed.</param>
/// <param name="Rejected">How many tasks were rejected.</param>
/// <param name="Cancelled">How many tasks were cancelled.</param>
/// <param name="Expired">How many tasks expired.</param>
/// <param name="Escalated">How many escalations were applied.</param>
/// <param name="Reminders">How many reminders fired.</param>
/// <param name="Reassignments">How many reassignments occurred.</param>
public sealed record HumanTaskMetricsSnapshot(
    long Created,
    long Completed,
    long Rejected,
    long Cancelled,
    long Expired,
    long Escalated,
    long Reminders,
    long Reassignments);

/// <summary>
/// Thread-safe counters for the human task engine, incremented by the runtime as tasks progress. A diagnostic
/// aid — reading the counters never affects behaviour.
/// </summary>
public sealed class HumanTaskMetrics
{
    private long _created;
    private long _completed;
    private long _rejected;
    private long _cancelled;
    private long _expired;
    private long _escalated;
    private long _reminders;
    private long _reassignments;

    /// <summary>Records that a task was created.</summary>
    public void RecordCreated() => Interlocked.Increment(ref _created);

    /// <summary>Records that a task was completed.</summary>
    public void RecordCompleted() => Interlocked.Increment(ref _completed);

    /// <summary>Records that a task was rejected.</summary>
    public void RecordRejected() => Interlocked.Increment(ref _rejected);

    /// <summary>Records that a task was cancelled.</summary>
    public void RecordCancelled() => Interlocked.Increment(ref _cancelled);

    /// <summary>Records that a task expired.</summary>
    public void RecordExpired() => Interlocked.Increment(ref _expired);

    /// <summary>Records that an escalation was applied.</summary>
    public void RecordEscalated() => Interlocked.Increment(ref _escalated);

    /// <summary>Records that a reminder fired.</summary>
    public void RecordReminder() => Interlocked.Increment(ref _reminders);

    /// <summary>Records that a task was reassigned.</summary>
    public void RecordReassigned() => Interlocked.Increment(ref _reassignments);

    /// <summary>Reads the current counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public HumanTaskMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _created),
        Interlocked.Read(ref _completed),
        Interlocked.Read(ref _rejected),
        Interlocked.Read(ref _cancelled),
        Interlocked.Read(ref _expired),
        Interlocked.Read(ref _escalated),
        Interlocked.Read(ref _reminders),
        Interlocked.Read(ref _reassignments));
}
