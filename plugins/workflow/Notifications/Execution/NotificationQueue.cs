using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Notifications.Diagnostics;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.Notifications.Persistence;

namespace FactoryOS.Plugins.Workflow.Notifications.Execution;

/// <summary>
/// The delivery queue: notifications are enqueued here when produced and drained from here, best-first
/// (priority then schedule), when they become due. Enqueueing records the notification, raises the queued event
/// and counts it. The queue holds no state of its own beyond the store — it is the store viewed as a work queue.
/// </summary>
public sealed class NotificationQueue
{
    private readonly INotificationStore _store;
    private readonly INotificationHistoryRepository _history;
    private readonly INotificationEventSink _events;
    private readonly NotificationMetrics _metrics;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="NotificationQueue"/> class.</summary>
    /// <param name="store">The notification store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="metrics">The metrics counters.</param>
    /// <param name="clock">The clock.</param>
    public NotificationQueue(
        INotificationStore store,
        INotificationHistoryRepository history,
        INotificationEventSink events,
        NotificationMetrics metrics,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _history = history;
        _events = events;
        _metrics = metrics;
        _clock = clock;
    }

    /// <summary>Enqueues a notification for delivery.</summary>
    /// <param name="notification">The notification to enqueue.</param>
    public void Enqueue(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var now = _clock.UtcNow;
        _store.Save(notification);
        _history.Append(new NotificationHistoryEntry(
            notification.Id, NotificationHistoryAction.Queued, "queue", null, now));
        _events.Publish(new NotificationQueued(
            notification.Id, notification.Tenant, now, notification.Channel, notification.Category));
        _metrics.RecordQueued();
    }

    /// <summary>Returns the notifications that are due at or before the given time, best-first.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="max">The maximum number to return.</param>
    /// <returns>The due notifications.</returns>
    public IReadOnlyList<Notification> DequeueDue(DateTimeOffset nowUtc, int max) => _store.ListDue(nowUtc, max);

    /// <summary>Gets the notifications still waiting in the queue.</summary>
    /// <returns>The pending notifications.</returns>
    public IReadOnlyCollection<Notification> Pending() => _store.ListPending();
}

/// <summary>
/// The dead-letter queue: notifications whose retry budget was exhausted land here so they are not lost and can
/// be inspected or replayed. Requeueing a dead-lettered notification puts it back on the delivery queue for
/// another set of attempts.
/// </summary>
public sealed class DeadLetterQueue
{
    private readonly INotificationStore _store;
    private readonly INotificationHistoryRepository _history;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="DeadLetterQueue"/> class.</summary>
    /// <param name="store">The notification store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="clock">The clock.</param>
    public DeadLetterQueue(
        INotificationStore store, INotificationHistoryRepository history, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _history = history;
        _clock = clock;
    }

    /// <summary>Gets the dead-lettered notifications.</summary>
    /// <returns>The dead-lettered notifications.</returns>
    public IReadOnlyCollection<Notification> List() => _store.ListByStatus(NotificationStatus.DeadLettered);

    /// <summary>Requeues a dead-lettered notification for another set of attempts.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <returns>The requeued notification, or <see langword="null"/> when it is unknown or not dead-lettered.</returns>
    public Notification? Requeue(Guid notificationId)
    {
        var notification = _store.Get(notificationId);
        if (notification is null || !notification.Requeue(_clock.UtcNow))
        {
            return null;
        }

        _store.Save(notification);
        _history.Append(new NotificationHistoryEntry(
            notification.Id, NotificationHistoryAction.Queued, "dead-letter", "requeued", _clock.UtcNow));
        return notification;
    }
}
