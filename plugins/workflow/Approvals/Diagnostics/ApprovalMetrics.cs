using System.Threading;

namespace FactoryOS.Plugins.Workflow.Approvals.Diagnostics;

/// <summary>An immutable snapshot of the approval engine's counters.</summary>
/// <param name="Created">How many approvals were created.</param>
/// <param name="Approved">How many approvals finished approved.</param>
/// <param name="Rejected">How many approvals finished rejected.</param>
/// <param name="Cancelled">How many approvals were cancelled.</param>
/// <param name="Expired">How many approvals expired.</param>
/// <param name="Votes">How many participant votes were cast.</param>
/// <param name="Escalated">How many escalations were applied.</param>
/// <param name="Reminders">How many reminders fired.</param>
public sealed record ApprovalMetricsSnapshot(
    long Created,
    long Approved,
    long Rejected,
    long Cancelled,
    long Expired,
    long Votes,
    long Escalated,
    long Reminders);

/// <summary>
/// Thread-safe counters for the approval engine, incremented by the runtime as approvals progress. A
/// diagnostic aid — reading the counters never affects behaviour.
/// </summary>
public sealed class ApprovalMetrics
{
    private long _created;
    private long _approved;
    private long _rejected;
    private long _cancelled;
    private long _expired;
    private long _votes;
    private long _escalated;
    private long _reminders;

    /// <summary>Records that an approval was created.</summary>
    public void RecordCreated() => Interlocked.Increment(ref _created);

    /// <summary>Records that an approval finished approved.</summary>
    public void RecordApproved() => Interlocked.Increment(ref _approved);

    /// <summary>Records that an approval finished rejected.</summary>
    public void RecordRejected() => Interlocked.Increment(ref _rejected);

    /// <summary>Records that an approval was cancelled.</summary>
    public void RecordCancelled() => Interlocked.Increment(ref _cancelled);

    /// <summary>Records that an approval expired.</summary>
    public void RecordExpired() => Interlocked.Increment(ref _expired);

    /// <summary>Records that a participant vote was cast.</summary>
    public void RecordVote() => Interlocked.Increment(ref _votes);

    /// <summary>Records that an escalation was applied.</summary>
    public void RecordEscalated() => Interlocked.Increment(ref _escalated);

    /// <summary>Records that a reminder fired.</summary>
    public void RecordReminder() => Interlocked.Increment(ref _reminders);

    /// <summary>Reads the current counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public ApprovalMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _created),
        Interlocked.Read(ref _approved),
        Interlocked.Read(ref _rejected),
        Interlocked.Read(ref _cancelled),
        Interlocked.Read(ref _expired),
        Interlocked.Read(ref _votes),
        Interlocked.Read(ref _escalated),
        Interlocked.Read(ref _reminders));
}
