using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Events;

/// <summary>The base of a notification lifecycle event raised by the runtime and published onto the event bus.</summary>
/// <param name="NotificationId">The notification the event concerns.</param>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OccurredOnUtc">When the event occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
public abstract record NotificationEvent(
    Guid NotificationId,
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    NotificationChannel Channel,
    NotificationCategory Category);

/// <summary>Raised when a notification is queued for delivery.</summary>
/// <param name="NotificationId">The notification.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
public sealed record NotificationQueued(
    Guid NotificationId, string Tenant, DateTimeOffset OccurredOnUtc, NotificationChannel Channel, NotificationCategory Category)
    : NotificationEvent(NotificationId, Tenant, OccurredOnUtc, Channel, Category);

/// <summary>Raised when a notification is handed to its channel for delivery.</summary>
/// <param name="NotificationId">The notification.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
public sealed record NotificationSending(
    Guid NotificationId, string Tenant, DateTimeOffset OccurredOnUtc, NotificationChannel Channel, NotificationCategory Category)
    : NotificationEvent(NotificationId, Tenant, OccurredOnUtc, Channel, Category);

/// <summary>Raised when a notification is accepted by its channel provider.</summary>
/// <param name="NotificationId">The notification.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
public sealed record NotificationSent(
    Guid NotificationId, string Tenant, DateTimeOffset OccurredOnUtc, NotificationChannel Channel, NotificationCategory Category)
    : NotificationEvent(NotificationId, Tenant, OccurredOnUtc, Channel, Category);

/// <summary>Raised when a notification is delivered to its recipient.</summary>
/// <param name="NotificationId">The notification.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
public sealed record NotificationDelivered(
    Guid NotificationId, string Tenant, DateTimeOffset OccurredOnUtc, NotificationChannel Channel, NotificationCategory Category)
    : NotificationEvent(NotificationId, Tenant, OccurredOnUtc, Channel, Category);

/// <summary>Raised when a recipient reads a notification.</summary>
/// <param name="NotificationId">The notification.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
public sealed record NotificationRead(
    Guid NotificationId, string Tenant, DateTimeOffset OccurredOnUtc, NotificationChannel Channel, NotificationCategory Category)
    : NotificationEvent(NotificationId, Tenant, OccurredOnUtc, Channel, Category);

/// <summary>
/// Raised when a delivery attempt fails. <paramref name="DeadLettered"/> is <see langword="true"/> on the
/// final failure, when the retry budget is exhausted and the notification is moved to the dead-letter queue.
/// </summary>
/// <param name="NotificationId">The notification.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
/// <param name="Reason">Why the attempt failed.</param>
/// <param name="AttemptNumber">The 1-based attempt number that failed.</param>
/// <param name="DeadLettered">Whether this failure exhausted the retries and dead-lettered the notification.</param>
public sealed record NotificationFailed(
    Guid NotificationId,
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    NotificationChannel Channel,
    NotificationCategory Category,
    NotificationFailureReason Reason,
    int AttemptNumber,
    bool DeadLettered)
    : NotificationEvent(NotificationId, Tenant, OccurredOnUtc, Channel, Category);

/// <summary>Raised when a retry is scheduled after a failed attempt.</summary>
/// <param name="NotificationId">The notification.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
/// <param name="AttemptNumber">The attempt number just completed.</param>
/// <param name="NextAttemptOnUtc">When the next attempt becomes due.</param>
public sealed record NotificationRetried(
    Guid NotificationId,
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    NotificationChannel Channel,
    NotificationCategory Category,
    int AttemptNumber,
    DateTimeOffset NextAttemptOnUtc)
    : NotificationEvent(NotificationId, Tenant, OccurredOnUtc, Channel, Category);

/// <summary>Raised when a notification is cancelled before delivery.</summary>
/// <param name="NotificationId">The notification.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
public sealed record NotificationCancelled(
    Guid NotificationId, string Tenant, DateTimeOffset OccurredOnUtc, NotificationChannel Channel, NotificationCategory Category)
    : NotificationEvent(NotificationId, Tenant, OccurredOnUtc, Channel, Category);

/// <summary>Raised when a notification expires undelivered.</summary>
/// <param name="NotificationId">The notification.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
public sealed record NotificationExpired(
    Guid NotificationId, string Tenant, DateTimeOffset OccurredOnUtc, NotificationChannel Channel, NotificationCategory Category)
    : NotificationEvent(NotificationId, Tenant, OccurredOnUtc, Channel, Category);

/// <summary>Raised when a notification is suppressed by a rule, preference or digest fold.</summary>
/// <param name="NotificationId">The notification.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Category">The category.</param>
public sealed record NotificationSuppressed(
    Guid NotificationId, string Tenant, DateTimeOffset OccurredOnUtc, NotificationChannel Channel, NotificationCategory Category)
    : NotificationEvent(NotificationId, Tenant, OccurredOnUtc, Channel, Category);

/// <summary>Receives notification lifecycle events raised by the runtime. The seam onto the platform event bus.</summary>
public interface INotificationEventSink
{
    /// <summary>Publishes a notification event.</summary>
    /// <param name="notificationEvent">The event to publish.</param>
    void Publish(NotificationEvent notificationEvent);
}

/// <summary>An in-memory <see cref="INotificationEventSink"/> that records published events for inspection.</summary>
public sealed class InMemoryNotificationEventSink : INotificationEventSink
{
    private readonly ConcurrentQueue<NotificationEvent> _events = new();

    /// <summary>Gets the published events in order.</summary>
    public IReadOnlyList<NotificationEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Publish(NotificationEvent notificationEvent)
    {
        ArgumentNullException.ThrowIfNull(notificationEvent);
        _events.Enqueue(notificationEvent);
    }
}
