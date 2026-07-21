using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.Notifications.Persistence;

namespace FactoryOS.Plugins.Workflow.Notifications.Execution;

/// <summary>A summary of what one queue-processing pass did.</summary>
/// <param name="Processed">How many notifications were taken off the queue.</param>
/// <param name="Delivered">How many were delivered.</param>
/// <param name="Retried">How many failed and were scheduled to retry.</param>
/// <param name="DeadLettered">How many failed and were dead-lettered.</param>
/// <param name="Expired">How many were expired before delivery.</param>
public sealed record NotificationProcessSummary(int Processed, int Delivered, int Retried, int DeadLettered, int Expired);

/// <summary>
/// Drains the delivery queue: it takes the due notifications best-first, expires any whose time-to-live has
/// passed, dispatches the rest, and routes each failure through the retry service (which schedules a retry or
/// dead-letters it). One pass processes at most the configured batch size, so a large backlog is worked off
/// over several passes rather than in one unbounded burst.
/// </summary>
public sealed class NotificationQueueProcessor
{
    private readonly NotificationQueue _queue;
    private readonly NotificationDispatcher _dispatcher;
    private readonly NotificationRetryService _retry;
    private readonly INotificationStore _store;
    private readonly INotificationHistoryRepository _history;
    private readonly INotificationEventSink _events;
    private readonly NotificationEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="NotificationQueueProcessor"/> class.</summary>
    /// <param name="queue">The delivery queue.</param>
    /// <param name="dispatcher">The dispatcher.</param>
    /// <param name="retry">The retry service.</param>
    /// <param name="store">The notification store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public NotificationQueueProcessor(
        NotificationQueue queue,
        NotificationDispatcher dispatcher,
        NotificationRetryService retry,
        INotificationStore store,
        INotificationHistoryRepository history,
        INotificationEventSink events,
        NotificationEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(retry);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _queue = queue;
        _dispatcher = dispatcher;
        _retry = retry;
        _store = store;
        _history = history;
        _events = events;
        _options = options;
        _clock = clock;
    }

    /// <summary>Processes the notifications that are due now.</summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>A summary of the pass.</returns>
    public async Task<NotificationProcessSummary> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var due = _queue.DequeueDue(now, _options.DueWorkBatchSize);

        int processed = 0, delivered = 0, retried = 0, deadLettered = 0, expired = 0;
        foreach (var notification in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            if (notification.ExpiresOnUtc is { } expiry && expiry <= now && notification.Expire())
            {
                _store.Save(notification);
                _events.Publish(new NotificationExpired(
                    notification.Id, notification.Tenant, now, notification.Channel, notification.Category));
                _history.Append(new NotificationHistoryEntry(
                    notification.Id, NotificationHistoryAction.Expired, "processor", null, now));
                expired++;
                continue;
            }

            var result = await _dispatcher.DispatchAsync(notification, cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                delivered++;
            }
            else if (_retry.HandleFailure(notification, result.Reason ?? NotificationFailureReason.TransportError))
            {
                retried++;
            }
            else
            {
                deadLettered++;
            }
        }

        return new NotificationProcessSummary(processed, delivered, retried, deadLettered, expired);
    }
}
