namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a worker was staffed on a shift without a valid required certification — either they
/// never held it (<c>Missing</c>) or it had expired by the shift start (<c>Expired</c>). Notification,
/// scheduling, compliance and dashboards consume it without referencing the HR module.
/// </summary>
public sealed record CertificationGapDetected : IntegrationEvent
{
    /// <summary>The tenant the worker belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The shift the gap was found on.</summary>
    public required string ShiftId { get; init; }

    /// <summary>The worker with the certification gap.</summary>
    public required string WorkerId { get; init; }

    /// <summary>The certification the shift required.</summary>
    public required string RequiredCertification { get; init; }

    /// <summary>Why it is a gap: <c>Missing</c> or <c>Expired</c>.</summary>
    public required string Reason { get; init; }

    /// <summary>The certification's expiry, when the worker held it (for an <c>Expired</c> gap); otherwise null.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>When the shift starts.</summary>
    public DateTimeOffset ShiftStart { get; init; }

    /// <summary>The id of the <see cref="ShiftStaffed"/> that triggered this detection.</summary>
    public Guid SourceEventId { get; init; }
}
