using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Execution;

namespace FactoryOS.Plugins.Workflow.Notifications.Integration;

/// <summary>
/// A generic seam that turns any module's event object into a notification without a bespoke subscriber: the
/// caller hands over the event, the owning tenant, a category and (optionally) explicit recipients, and the
/// subscriber derives the subject from the event type and the body from its text. Modules that do not have a
/// dedicated subscriber use this to participate in the notification engine.
/// </summary>
public sealed class GenericEventSubscriber
{
    private readonly NotificationEngine _engine;
    private readonly NotificationEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="GenericEventSubscriber"/> class.</summary>
    /// <param name="engine">The notification engine.</param>
    /// <param name="options">The engine options.</param>
    public GenericEventSubscriber(NotificationEngine engine, NotificationEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(options);
        _engine = engine;
        _options = options;
    }

    /// <summary>Produces a notification from an arbitrary event object.</summary>
    /// <param name="domainEvent">The source event.</param>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="category">The notification category.</param>
    /// <param name="recipients">The explicit recipients, or <see langword="null"/> to rely on subscriptions.</param>
    /// <param name="sourceKey">An optional source key used to match subscriptions.</param>
    /// <param name="priority">The notification priority.</param>
    /// <returns>The deliverable notifications produced (empty when subscription is disabled).</returns>
    public IReadOnlyList<Notification> Handle(
        object domainEvent,
        string tenant,
        NotificationCategory category,
        IReadOnlyList<NotificationAssignment>? recipients = null,
        string? sourceKey = null,
        NotificationPriority priority = NotificationPriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        if (!_options.SubscribeToEngineEvents)
        {
            return [];
        }

        return _engine.Notify(
            new NotificationRequest
            {
                Category = category,
                Source = "generic",
                SourceKey = sourceKey,
                Priority = priority,
                Recipients = recipients ?? [],
                Subject = domainEvent.GetType().Name,
                Body = domainEvent.ToString() ?? domainEvent.GetType().Name,
            },
            new NotificationContext(tenant));
    }
}
