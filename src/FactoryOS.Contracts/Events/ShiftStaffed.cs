namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a worker was staffed on a shift that may require a certification. Any module consumes it
/// without referencing the producer; the HR module checks the worker's certification against the requirement.
/// </summary>
public sealed record ShiftStaffed : IntegrationEvent
{
    /// <summary>The tenant the shift belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The shift identifier.</summary>
    public required string ShiftId { get; init; }

    /// <summary>The worker staffed on the shift.</summary>
    public required string WorkerId { get; init; }

    /// <summary>The certification the shift requires, if any. Empty means no certification is required.</summary>
    public string RequiredCertification { get; init; } = string.Empty;

    /// <summary>When the shift starts — the instant the certification must be valid at.</summary>
    public DateTimeOffset ShiftStart { get; init; }
}
