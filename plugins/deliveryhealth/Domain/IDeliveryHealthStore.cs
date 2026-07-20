namespace FactoryOS.Plugins.DeliveryHealth.Domain;

/// <summary>
/// The tenant-scoped read model of notification-delivery health: per-transport tallies and a bounded list of recent
/// failure details. Recording is idempotent by the delivery event's id, so redelivery does not double-count.
/// </summary>
public interface IDeliveryHealthStore
{
    /// <summary>
    /// Records a delivery outcome for a tenant, keyed by the delivery event's id for idempotency.
    /// </summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="sourceEventId">The delivery event's id; a repeat is a no-op.</param>
    /// <param name="transport">The transport the delivery was attempted on.</param>
    /// <param name="channel">The logical channel the notification targeted.</param>
    /// <param name="subject">A human-readable description of the notification.</param>
    /// <param name="delivered">Whether the delivery succeeded.</param>
    /// <param name="detail">The connector's outcome detail, if any.</param>
    /// <param name="at">When the delivery was attempted.</param>
    /// <returns>An atomic snapshot: whether it was newly recorded and the transport's tallies and failure streak.</returns>
    RecordOutcome Record(string tenant, Guid sourceEventId, string transport, string channel, string subject, bool delivered, string? detail, DateTimeOffset at);

    /// <summary>Returns a tenant's per-transport delivery tallies, ordered by transport.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The per-transport health tallies.</returns>
    IReadOnlyList<TransportHealth> ForTenant(string tenant);

    /// <summary>Returns a tenant's most recent failed deliveries, newest first.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="max">The maximum number to return.</param>
    /// <returns>The recent failures.</returns>
    IReadOnlyList<DeliveryFailure> RecentFailures(string tenant, int max);
}
