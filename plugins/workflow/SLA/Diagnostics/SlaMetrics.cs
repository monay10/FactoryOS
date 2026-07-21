using System.Threading;

namespace FactoryOS.Plugins.Workflow.SLA.Diagnostics;

/// <summary>An immutable snapshot of the SLA engine's counters.</summary>
/// <param name="Started">How many SLAs started.</param>
/// <param name="Met">How many finished within their deadline.</param>
/// <param name="Breached">How many missed their deadline.</param>
/// <param name="TimedOut">How many hit their hard timeout.</param>
/// <param name="Cancelled">How many were cancelled.</param>
/// <param name="Reminders">How many reminders fired.</param>
/// <param name="Escalations">How many escalations fired.</param>
/// <param name="Paused">How many times a clock was stopped.</param>
/// <param name="Resumed">How many times a clock was restarted.</param>
public sealed record SlaMetricsSnapshot(
    long Started,
    long Met,
    long Breached,
    long TimedOut,
    long Cancelled,
    long Reminders,
    long Escalations,
    long Paused,
    long Resumed);

/// <summary>
/// Thread-safe counters for the SLA engine, incremented by the runtime as SLAs progress. A diagnostic aid —
/// reading the counters never affects behaviour.
/// </summary>
public sealed class SlaMetrics
{
    private long _started;
    private long _met;
    private long _breached;
    private long _timedOut;
    private long _cancelled;
    private long _reminders;
    private long _escalations;
    private long _paused;
    private long _resumed;

    /// <summary>Records that an SLA started.</summary>
    public void RecordStarted() => Interlocked.Increment(ref _started);

    /// <summary>Records that an SLA finished within its deadline.</summary>
    public void RecordMet() => Interlocked.Increment(ref _met);

    /// <summary>Records that an SLA missed its deadline.</summary>
    public void RecordBreached() => Interlocked.Increment(ref _breached);

    /// <summary>Records that an SLA hit its hard timeout.</summary>
    public void RecordTimedOut() => Interlocked.Increment(ref _timedOut);

    /// <summary>Records that an SLA was cancelled.</summary>
    public void RecordCancelled() => Interlocked.Increment(ref _cancelled);

    /// <summary>Records that a reminder fired.</summary>
    public void RecordReminder() => Interlocked.Increment(ref _reminders);

    /// <summary>Records that an escalation fired.</summary>
    public void RecordEscalation() => Interlocked.Increment(ref _escalations);

    /// <summary>Records that a clock was stopped.</summary>
    public void RecordPaused() => Interlocked.Increment(ref _paused);

    /// <summary>Records that a clock was restarted.</summary>
    public void RecordResumed() => Interlocked.Increment(ref _resumed);

    /// <summary>Reads the current counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public SlaMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _started),
        Interlocked.Read(ref _met),
        Interlocked.Read(ref _breached),
        Interlocked.Read(ref _timedOut),
        Interlocked.Read(ref _cancelled),
        Interlocked.Read(ref _reminders),
        Interlocked.Read(ref _escalations),
        Interlocked.Read(ref _paused),
        Interlocked.Read(ref _resumed));
}
