using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Diagnostics;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.Notifications.Persistence;

namespace FactoryOS.Plugins.Workflow.Notifications.Execution;

/// <summary>
/// The heart of the notification engine: it turns a request into notifications (through the router), suppresses
/// the ones a rule or preference blocked, enqueues the rest, and — for immediate delivery — drains the queue at
/// once. It also marks notifications read, cancels them, folds digests, and runs the due-work pass. It composes
/// the queue, dispatcher, retry service and stores; it produces notifications and never reaches back into the
/// workflow, human task, approval or forms engines.
/// </summary>
public sealed class NotificationRuntime
{
    private readonly NotificationRouter _router;
    private readonly NotificationQueue _queue;
    private readonly NotificationQueueProcessor _processor;
    private readonly INotificationRepository _definitions;
    private readonly INotificationStore _store;
    private readonly INotificationHistoryRepository _history;
    private readonly INotificationEventSink _events;
    private readonly NotificationMetrics _metrics;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="NotificationRuntime"/> class.</summary>
    /// <param name="router">The router.</param>
    /// <param name="queue">The delivery queue.</param>
    /// <param name="processor">The queue processor.</param>
    /// <param name="definitions">The definition repository.</param>
    /// <param name="store">The notification store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="metrics">The metrics counters.</param>
    /// <param name="clock">The clock.</param>
    public NotificationRuntime(
        NotificationRouter router,
        NotificationQueue queue,
        NotificationQueueProcessor processor,
        INotificationRepository definitions,
        INotificationStore store,
        INotificationHistoryRepository history,
        INotificationEventSink events,
        NotificationMetrics metrics,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(clock);
        _router = router;
        _queue = queue;
        _processor = processor;
        _definitions = definitions;
        _store = store;
        _history = history;
        _events = events;
        _metrics = metrics;
        _clock = clock;
    }

    /// <summary>Registers a notification definition.</summary>
    /// <param name="definition">The definition.</param>
    public void Register(NotificationDefinition definition) => _definitions.Register(definition);

    /// <summary>
    /// Produces notifications for a request and queues the deliverable ones (suppressing the rest). This does no
    /// delivery — it is safe to call from a synchronous event handler; the queued notifications are delivered by
    /// the next <see cref="ProcessDueAsync"/> pass.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="context">The context.</param>
    /// <returns>The deliverable notifications produced.</returns>
    public IReadOnlyList<Notification> Notify(NotificationRequest request, NotificationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var definition = request.DefinitionKey is null ? null : _definitions.Get(request.DefinitionKey);
        var routed = _router.Build(request, definition, context, _clock.UtcNow);

        foreach (var suppressedNotification in routed.Suppressed)
        {
            RecordSuppressed(suppressedNotification);
        }

        foreach (var notification in routed.Deliverable)
        {
            _queue.Enqueue(notification);
        }

        return routed.Deliverable;
    }

    /// <summary>Produces notifications for a request, queues them, and delivers the immediate ones at once.</summary>
    /// <param name="request">The request.</param>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">A token to cancel any immediate delivery.</param>
    /// <returns>The deliverable notifications produced (their status reflects any immediate delivery).</returns>
    public async Task<IReadOnlyList<Notification>> NotifyAsync(
        NotificationRequest request, NotificationContext context, CancellationToken cancellationToken = default)
    {
        var deliverable = Notify(request, context);
        if (deliverable.Any(notification => notification.DeliveryPolicy == NotificationDeliveryPolicy.Immediate))
        {
            await _processor.ProcessDueAsync(cancellationToken).ConfigureAwait(false);
        }

        return deliverable;
    }

    /// <summary>Runs one delivery pass over the notifications that are due now.</summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>A summary of the pass.</returns>
    public Task<NotificationProcessSummary> ProcessDueAsync(CancellationToken cancellationToken = default) =>
        _processor.ProcessDueAsync(cancellationToken);

    /// <summary>Marks a notification read by its recipient.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <returns>The updated notification, or <see langword="null"/> when unknown or not readable.</returns>
    public Notification? MarkRead(Guid notificationId)
    {
        var notification = _store.Get(notificationId);
        var now = _clock.UtcNow;
        if (notification is null || !notification.MarkRead(now))
        {
            return null;
        }

        _store.Save(notification);
        _events.Publish(new NotificationRead(
            notification.Id, notification.Tenant, now, notification.Channel, notification.Category));
        _history.Append(new NotificationHistoryEntry(
            notification.Id, NotificationHistoryAction.Read, notification.RecipientUserId, null, now));
        _metrics.RecordRead();
        return notification;
    }

    /// <summary>Cancels a notification before it is delivered.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <param name="actor">Who is cancelling it.</param>
    /// <returns>The cancelled notification, or <see langword="null"/> when unknown or already delivered.</returns>
    public Notification? Cancel(Guid notificationId, string? actor = null)
    {
        var notification = _store.Get(notificationId);
        var now = _clock.UtcNow;
        if (notification is null || !notification.Cancel())
        {
            return null;
        }

        _store.Save(notification);
        _events.Publish(new NotificationCancelled(
            notification.Id, notification.Tenant, now, notification.Channel, notification.Category));
        _history.Append(new NotificationHistoryEntry(
            notification.Id, NotificationHistoryAction.Cancelled, actor ?? "system", null, now));
        _metrics.RecordCancelled();
        return notification;
    }

    /// <summary>
    /// Folds the pending digest and batch notifications into one combined message per recipient and channel,
    /// suppresses the folded originals, and queues the digests for immediate delivery.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the delivery of the produced digests.</param>
    /// <returns>The number of digest notifications produced.</returns>
    public async Task<int> FlushDigestsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var batched = _store.ListPending()
            .Where(notification => notification.DeliveryPolicy
                is NotificationDeliveryPolicy.Digest or NotificationDeliveryPolicy.Batch)
            .GroupBy(notification => (notification.Tenant, notification.RecipientUserId, notification.Channel));

        var produced = 0;
        foreach (var group in batched)
        {
            var batch = new NotificationBatch(group.Key.Tenant, group.Key.RecipientUserId, group.Key.Channel);
            foreach (var member in group)
            {
                batch.Add(member);
            }

            var digest = new Notification(
                group.Key.Tenant,
                NotificationCategory.Digest,
                NotificationPriority.Normal,
                group.Key.Channel,
                group.Key.RecipientUserId,
                batch.Members[0].RecipientAddress,
                batch.ComposeBody(),
                now,
                subject: $"{batch.Members.Count} notifications",
                source: "digest");

            _queue.Enqueue(digest);
            foreach (var member in batch.Members)
            {
                RecordSuppressed(member);
            }

            produced++;
        }

        if (produced > 0)
        {
            await _processor.ProcessDueAsync(cancellationToken).ConfigureAwait(false);
        }

        return produced;
    }

    /// <summary>Gets a notification by id.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <returns>The notification, or <see langword="null"/> when not found.</returns>
    public Notification? Get(Guid notificationId) => _store.Get(notificationId);

    /// <summary>Lists the notifications addressed to a recipient.</summary>
    /// <param name="userId">The recipient user id.</param>
    /// <returns>The recipient's notifications.</returns>
    public IReadOnlyCollection<Notification> ListForRecipient(string userId) => _store.ListByRecipient(userId);

    /// <summary>Gets the history entries of a notification, oldest first.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <returns>The history entries.</returns>
    public IReadOnlyList<NotificationHistoryEntry> GetHistory(Guid notificationId) =>
        _history.ByNotification(notificationId);

    private void RecordSuppressed(Notification notification)
    {
        notification.Suppress();
        _store.Save(notification);
        _events.Publish(new NotificationSuppressed(
            notification.Id, notification.Tenant, _clock.UtcNow, notification.Channel, notification.Category));
        _history.Append(new NotificationHistoryEntry(
            notification.Id, NotificationHistoryAction.Suppressed, "runtime", null, _clock.UtcNow));
        _metrics.RecordSuppressed();
    }
}
