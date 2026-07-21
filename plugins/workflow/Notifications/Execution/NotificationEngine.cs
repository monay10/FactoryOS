using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Diagnostics;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Persistence;

namespace FactoryOS.Plugins.Workflow.Notifications.Execution;

/// <summary>
/// The public entry point to the notification engine. It registers definitions, templates, preferences and
/// subscriptions; produces notifications from a request; runs the delivery queue; marks notifications read;
/// cancels them; folds digests; inspects and replays the dead-letter queue; and reads notifications, history and
/// metrics back. It composes the runtime and the stores. It listens to the workflow, human task, approval and
/// forms engines through their events (via the integration subscribers) and never modifies them.
/// </summary>
public sealed class NotificationEngine
{
    private readonly NotificationRuntime _runtime;
    private readonly DeadLetterQueue _deadLetters;
    private readonly INotificationTemplateRepository _templates;
    private readonly INotificationPreferenceStore _preferences;
    private readonly INotificationSubscriptionStore _subscriptions;
    private readonly NotificationMetrics _metrics;

    /// <summary>Initializes a new instance of the <see cref="NotificationEngine"/> class.</summary>
    /// <param name="runtime">The notification runtime.</param>
    /// <param name="deadLetters">The dead-letter queue.</param>
    /// <param name="templates">The template repository.</param>
    /// <param name="preferences">The preference store.</param>
    /// <param name="subscriptions">The subscription store.</param>
    /// <param name="metrics">The metrics counters.</param>
    public NotificationEngine(
        NotificationRuntime runtime,
        DeadLetterQueue deadLetters,
        INotificationTemplateRepository templates,
        INotificationPreferenceStore preferences,
        INotificationSubscriptionStore subscriptions,
        NotificationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(deadLetters);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(metrics);
        _runtime = runtime;
        _deadLetters = deadLetters;
        _templates = templates;
        _preferences = preferences;
        _subscriptions = subscriptions;
        _metrics = metrics;
    }

    /// <summary>Registers a notification definition.</summary>
    /// <param name="definition">The definition.</param>
    public void Register(NotificationDefinition definition) => _runtime.Register(definition);

    /// <summary>Registers a message template.</summary>
    /// <param name="template">The template.</param>
    public void RegisterTemplate(NotificationTemplate template) => _templates.Register(template);

    /// <summary>Sets a recipient's delivery preference.</summary>
    /// <param name="preference">The preference.</param>
    public void SetPreference(NotificationPreference preference) => _preferences.Set(preference);

    /// <summary>Adds a standing subscription.</summary>
    /// <param name="subscription">The subscription.</param>
    public void Subscribe(NotificationSubscription subscription) => _subscriptions.Add(subscription);

    /// <summary>
    /// Produces notifications for a request and queues the deliverable ones without delivering — safe to call
    /// from a synchronous event handler. Delivery happens on the next <see cref="ProcessDueAsync"/> pass.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="context">The context.</param>
    /// <returns>The deliverable notifications produced.</returns>
    public IReadOnlyList<Notification> Notify(NotificationRequest request, NotificationContext context) =>
        _runtime.Notify(request, context);

    /// <summary>Produces notifications for a request, queues them, and delivers the immediate ones at once.</summary>
    /// <param name="request">The request.</param>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">A token to cancel any immediate delivery.</param>
    /// <returns>The deliverable notifications produced.</returns>
    public Task<IReadOnlyList<Notification>> NotifyAsync(
        NotificationRequest request, NotificationContext context, CancellationToken cancellationToken = default) =>
        _runtime.NotifyAsync(request, context, cancellationToken);

    /// <summary>Runs one delivery pass over the notifications that are due now.</summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>A summary of the pass.</returns>
    public Task<NotificationProcessSummary> ProcessDueAsync(CancellationToken cancellationToken = default) =>
        _runtime.ProcessDueAsync(cancellationToken);

    /// <summary>Folds pending digest and batch notifications into combined messages and delivers them.</summary>
    /// <param name="cancellationToken">A token to cancel delivery of the produced digests.</param>
    /// <returns>The number of digest notifications produced.</returns>
    public Task<int> FlushDigestsAsync(CancellationToken cancellationToken = default) =>
        _runtime.FlushDigestsAsync(cancellationToken);

    /// <summary>Marks a notification read by its recipient.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <returns>The updated notification, or <see langword="null"/> when unknown or not readable.</returns>
    public Notification? MarkRead(Guid notificationId) => _runtime.MarkRead(notificationId);

    /// <summary>Cancels a notification before it is delivered.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <param name="actor">Who is cancelling it.</param>
    /// <returns>The cancelled notification, or <see langword="null"/> when unknown or already delivered.</returns>
    public Notification? Cancel(Guid notificationId, string? actor = null) => _runtime.Cancel(notificationId, actor);

    /// <summary>Requeues a dead-lettered notification for another set of attempts.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <returns>The requeued notification, or <see langword="null"/> when unknown or not dead-lettered.</returns>
    public Notification? RequeueDeadLetter(Guid notificationId) => _deadLetters.Requeue(notificationId);

    /// <summary>Gets the dead-lettered notifications.</summary>
    /// <returns>The dead-lettered notifications.</returns>
    public IReadOnlyCollection<Notification> DeadLetters() => _deadLetters.List();

    /// <summary>Gets a notification by id.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <returns>The notification, or <see langword="null"/> when not found.</returns>
    public Notification? GetNotification(Guid notificationId) => _runtime.Get(notificationId);

    /// <summary>Lists the notifications addressed to a recipient.</summary>
    /// <param name="userId">The recipient user id.</param>
    /// <returns>The recipient's notifications.</returns>
    public IReadOnlyCollection<Notification> ListForRecipient(string userId) => _runtime.ListForRecipient(userId);

    /// <summary>Gets the history entries of a notification, oldest first.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <returns>The history entries.</returns>
    public IReadOnlyList<NotificationHistoryEntry> GetHistory(Guid notificationId) =>
        _runtime.GetHistory(notificationId);

    /// <summary>Reads the engine's counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public NotificationMetricsSnapshot Snapshot() => _metrics.Snapshot();
}
