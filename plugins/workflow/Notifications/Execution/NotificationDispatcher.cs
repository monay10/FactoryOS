using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Notifications.Channels;
using FactoryOS.Plugins.Workflow.Notifications.Diagnostics;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.Notifications.Persistence;

namespace FactoryOS.Plugins.Workflow.Notifications.Execution;

/// <summary>The outcome of a single dispatch attempt.</summary>
/// <param name="Success">Whether the notification was delivered.</param>
/// <param name="Reason">Why the attempt failed, when it did.</param>
/// <param name="Error">A human-readable error, when the attempt failed.</param>
public sealed record DispatchResult(bool Success, NotificationFailureReason? Reason, string? Error);

/// <summary>
/// Delivers a single notification: it picks the channel sender for the notification's channel, begins a
/// delivery attempt, and on success marks the notification sent and delivered; on failure it records the
/// failure and reports it back so the retry service can decide what happens next. It emits the
/// sending / sent / delivered / failed lifecycle events and appends to the audit history. Scheduling retries and
/// dead-lettering are the retry service's job, not the dispatcher's.
/// </summary>
public sealed class NotificationDispatcher
{
    private readonly Dictionary<NotificationChannel, INotificationChannelSender> _senders;
    private readonly INotificationStore _store;
    private readonly INotificationHistoryRepository _history;
    private readonly INotificationEventSink _events;
    private readonly NotificationMetrics _metrics;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="NotificationDispatcher"/> class.</summary>
    /// <param name="senders">The channel senders, one per channel (later registrations win on a channel).</param>
    /// <param name="store">The notification store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="metrics">The metrics counters.</param>
    /// <param name="clock">The clock.</param>
    public NotificationDispatcher(
        IEnumerable<INotificationChannelSender> senders,
        INotificationStore store,
        INotificationHistoryRepository history,
        INotificationEventSink events,
        NotificationMetrics metrics,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(senders);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(clock);
        _senders = new Dictionary<NotificationChannel, INotificationChannelSender>();
        foreach (var sender in senders)
        {
            _senders[sender.Channel] = sender;
        }

        _store = store;
        _history = history;
        _events = events;
        _metrics = metrics;
        _clock = clock;
    }

    /// <summary>Attempts to deliver a notification once.</summary>
    /// <param name="notification">The notification to deliver.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The dispatch outcome.</returns>
    public async Task<DispatchResult> DispatchAsync(
        Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var now = _clock.UtcNow;
        var attempt = notification.BeginAttempt(now);
        _store.Save(notification);
        _events.Publish(new NotificationSending(
            notification.Id, notification.Tenant, now, notification.Channel, notification.Category));
        Record(notification, NotificationHistoryAction.Sending, "dispatcher", $"attempt {attempt}", now);

        if (!_senders.TryGetValue(notification.Channel, out var sender))
        {
            return Fail(notification, NotificationFailureReason.ChannelUnavailable,
                $"No sender registered for channel {notification.Channel}.", now);
        }

        var message = new OutboundNotification(
            notification.Id,
            notification.Tenant,
            notification.Channel,
            notification.RecipientAddress,
            notification.Subject,
            notification.Body,
            notification.Priority,
            notification.Attachments);

        var result = await sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return Fail(
                notification,
                result.FailureReason ?? NotificationFailureReason.TransportError,
                result.Error ?? "Delivery failed.",
                now);
        }

        notification.MarkSent(now, result.ProviderMessageId);
        _events.Publish(new NotificationSent(
            notification.Id, notification.Tenant, now, notification.Channel, notification.Category));
        Record(notification, NotificationHistoryAction.Sent, "dispatcher", result.ProviderMessageId, now);
        _metrics.RecordSent();

        notification.MarkDelivered(now);
        _events.Publish(new NotificationDelivered(
            notification.Id, notification.Tenant, now, notification.Channel, notification.Category));
        Record(notification, NotificationHistoryAction.Delivered, "dispatcher", null, now);
        _metrics.RecordDelivered();
        _store.Save(notification);

        return new DispatchResult(true, null, null);
    }

    private DispatchResult Fail(
        Notification notification, NotificationFailureReason reason, string error, DateTimeOffset now)
    {
        notification.RecordFailure(reason, error, now);
        _store.Save(notification);
        _metrics.RecordFailed();
        return new DispatchResult(false, reason, error);
    }

    private void Record(
        Notification notification, NotificationHistoryAction action, string actor, string? detail, DateTimeOffset now) =>
        _history.Append(new NotificationHistoryEntry(notification.Id, action, actor, detail, now));
}
