namespace FactoryOS.Contracts.Events;

/// <summary>
/// Emitted by the Quality module when a production line is placed under quarantine — a manual hold on a line pending
/// inspection. Shared vocabulary on the bus so Activity, Notification and dashboard modules react without referencing
/// Quality. Publication is idempotent: a line is announced quarantined only on the transition, so a repeat request
/// neither re-quarantines nor re-publishes.
/// </summary>
public sealed record QualityLineQuarantined : IntegrationEvent
{
    /// <summary>The tenant the line belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The production line or workstation placed under quarantine.</summary>
    public required string LineId { get; init; }

    /// <summary>Who quarantined the line (a user name or system actor), when known.</summary>
    public string? QuarantinedBy { get; init; }

    /// <summary>An optional reason recorded with the quarantine.</summary>
    public string? Reason { get; init; }
}
