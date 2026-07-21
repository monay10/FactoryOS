using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Notifications.Diagnostics;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.Notifications.Persistence;

namespace FactoryOS.Plugins.Workflow.Notifications.Execution;

/// <summary>
/// Decides what happens after a failed delivery attempt: while the notification still has attempts left in its
/// budget it schedules a retry with a linear back-off (and raises the failed and retried events); once the
/// budget is exhausted it dead-letters the notification (and raises a final failed event flagged as
/// dead-lettered). It never itself sends — it only moves the notification between the retry and dead-letter
/// states.
/// </summary>
public sealed class NotificationRetryService
{
    private readonly INotificationStore _store;
    private readonly INotificationHistoryRepository _history;
    private readonly INotificationEventSink _events;
    private readonly NotificationMetrics _metrics;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="NotificationRetryService"/> class.</summary>
    /// <param name="store">The notification store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="metrics">The metrics counters.</param>
    /// <param name="clock">The clock.</param>
    public NotificationRetryService(
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

    /// <summary>Handles a failed delivery attempt, scheduling a retry or dead-lettering the notification.</summary>
    /// <param name="notification">The notification whose attempt just failed.</param>
    /// <param name="reason">Why the attempt failed.</param>
    /// <returns><see langword="true"/> when a retry was scheduled; <see langword="false"/> when dead-lettered.</returns>
    public bool HandleFailure(Notification notification, NotificationFailureReason reason)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var now = _clock.UtcNow;

        if (notification.Attempts < notification.Retry.MaxAttempts)
        {
            var next = now + notification.Retry.DelayBeforeAttempt(notification.Attempts + 1);
            notification.ScheduleRetry(next);
            _store.Save(notification);

            PublishFailed(notification, reason, deadLettered: false, now);
            _events.Publish(new NotificationRetried(
                notification.Id, notification.Tenant, now, notification.Channel, notification.Category,
                notification.Attempts, next));
            _history.Append(new NotificationHistoryEntry(
                notification.Id, NotificationHistoryAction.Retried, "retry", $"next {next:O}", now));
            _metrics.RecordRetried();
            return true;
        }

        notification.DeadLetter();
        _store.Save(notification);
        PublishFailed(notification, reason, deadLettered: true, now);
        _history.Append(new NotificationHistoryEntry(
            notification.Id, NotificationHistoryAction.DeadLettered, "retry", reason.ToString(), now));
        _metrics.RecordDeadLettered();
        return false;
    }

    private void PublishFailed(
        Notification notification, NotificationFailureReason reason, bool deadLettered, DateTimeOffset now)
    {
        _events.Publish(new NotificationFailed(
            notification.Id, notification.Tenant, now, notification.Channel, notification.Category,
            reason, notification.Attempts, deadLettered));
        _history.Append(new NotificationHistoryEntry(
            notification.Id, NotificationHistoryAction.Failed, "retry", reason.ToString(), now));
    }
}
