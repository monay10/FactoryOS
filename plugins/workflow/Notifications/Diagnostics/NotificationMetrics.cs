using System.Threading;

namespace FactoryOS.Plugins.Workflow.Notifications.Diagnostics;

/// <summary>An immutable snapshot of the notification engine's counters.</summary>
/// <param name="Queued">How many notifications were queued.</param>
/// <param name="Sent">How many notifications were accepted by a channel.</param>
/// <param name="Delivered">How many notifications were delivered.</param>
/// <param name="Read">How many notifications were read.</param>
/// <param name="Failed">How many delivery attempts failed.</param>
/// <param name="Retried">How many retries were scheduled.</param>
/// <param name="DeadLettered">How many notifications were dead-lettered.</param>
/// <param name="Cancelled">How many notifications were cancelled.</param>
/// <param name="Suppressed">How many notifications were suppressed.</param>
public sealed record NotificationMetricsSnapshot(
    long Queued,
    long Sent,
    long Delivered,
    long Read,
    long Failed,
    long Retried,
    long DeadLettered,
    long Cancelled,
    long Suppressed);

/// <summary>
/// Thread-safe counters for the notification engine, incremented by the runtime as notifications progress. A
/// diagnostic aid — reading the counters never affects behaviour.
/// </summary>
public sealed class NotificationMetrics
{
    private long _queued;
    private long _sent;
    private long _delivered;
    private long _read;
    private long _failed;
    private long _retried;
    private long _deadLettered;
    private long _cancelled;
    private long _suppressed;

    /// <summary>Records that a notification was queued.</summary>
    public void RecordQueued() => Interlocked.Increment(ref _queued);

    /// <summary>Records that a notification was accepted by a channel.</summary>
    public void RecordSent() => Interlocked.Increment(ref _sent);

    /// <summary>Records that a notification was delivered.</summary>
    public void RecordDelivered() => Interlocked.Increment(ref _delivered);

    /// <summary>Records that a notification was read.</summary>
    public void RecordRead() => Interlocked.Increment(ref _read);

    /// <summary>Records that a delivery attempt failed.</summary>
    public void RecordFailed() => Interlocked.Increment(ref _failed);

    /// <summary>Records that a retry was scheduled.</summary>
    public void RecordRetried() => Interlocked.Increment(ref _retried);

    /// <summary>Records that a notification was dead-lettered.</summary>
    public void RecordDeadLettered() => Interlocked.Increment(ref _deadLettered);

    /// <summary>Records that a notification was cancelled.</summary>
    public void RecordCancelled() => Interlocked.Increment(ref _cancelled);

    /// <summary>Records that a notification was suppressed.</summary>
    public void RecordSuppressed() => Interlocked.Increment(ref _suppressed);

    /// <summary>Reads the current counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public NotificationMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _queued),
        Interlocked.Read(ref _sent),
        Interlocked.Read(ref _delivered),
        Interlocked.Read(ref _read),
        Interlocked.Read(ref _failed),
        Interlocked.Read(ref _retried),
        Interlocked.Read(ref _deadLettered),
        Interlocked.Read(ref _cancelled),
        Interlocked.Read(ref _suppressed));
}
