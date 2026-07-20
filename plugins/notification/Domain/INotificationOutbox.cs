namespace FactoryOS.Plugins.Notification.Domain;

/// <summary>
/// The tenant-scoped outbox of dispatched notifications: the durable record of every notification routed, and
/// the read model a UI queries for notification history. Recording is idempotent by the source event id.
/// </summary>
public interface INotificationOutbox
{
    /// <summary>
    /// Records a dispatched notification for a tenant, keyed by the source event id for idempotency.
    /// </summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="sourceEventId">The triggering event's id; a repeat is a no-op.</param>
    /// <param name="record">The notification record.</param>
    /// <returns><see langword="true"/> if newly recorded; <see langword="false"/> if already present.</returns>
    bool TryRecord(string tenant, Guid sourceEventId, NotificationRecord record);

    /// <summary>Returns a tenant's dispatched notifications, newest first.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The recorded notifications.</returns>
    IReadOnlyList<NotificationRecord> ForTenant(string tenant);
}
