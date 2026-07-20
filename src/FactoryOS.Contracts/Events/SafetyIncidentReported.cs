namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a safety incident was reported at a site. Any module consumes it without referencing the
/// producer; the Safety module folds it into a rolling incident window and may answer with a
/// <see cref="SafetyStandDownTriggered"/>.
/// </summary>
public sealed record SafetyIncidentReported : IntegrationEvent
{
    /// <summary>The tenant the site belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The site, area or line the incident occurred at.</summary>
    public required string SiteId { get; init; }

    /// <summary>The incident severity, from 1 (minor) to 5 (catastrophic).</summary>
    public int Severity { get; init; }

    /// <summary>The incident category (for example <c>Slip</c>, <c>Chemical</c>), if classified.</summary>
    public string? Category { get; init; }

    /// <summary>When the incident occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}
