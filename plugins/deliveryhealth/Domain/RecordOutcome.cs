namespace FactoryOS.Plugins.DeliveryHealth.Domain;

/// <summary>
/// The atomic snapshot returned from recording a delivery outcome — captured under the tenant lock so a consumer
/// can decide, race-free, whether the record was new and whether a degradation threshold was just crossed.
/// </summary>
/// <param name="Recorded">Whether this was a new record (<see langword="false"/> if the event id was a duplicate).</param>
/// <param name="Attempts">The transport's total attempts after this record.</param>
/// <param name="Delivered">The transport's total successful deliveries after this record.</param>
/// <param name="Failed">The transport's total failures after this record.</param>
/// <param name="ConsecutiveFailures">The transport's current consecutive-failure streak (reset to zero by any success).</param>
public readonly record struct RecordOutcome(
    bool Recorded,
    int Attempts,
    int Delivered,
    int Failed,
    int ConsecutiveFailures);
