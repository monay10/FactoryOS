namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a safety stand-down (work stoppage / review) is recommended for a site — because an
/// incident was severe enough on its own, or because incidents accumulated past the frequency threshold.
/// Maintenance, notification, scheduling and dashboards consume it without referencing the Safety module.
/// </summary>
public sealed record SafetyStandDownTriggered : IntegrationEvent
{
    /// <summary>The tenant the site belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The site the stand-down is recommended for.</summary>
    public required string SiteId { get; init; }

    /// <summary>Why the stand-down was triggered: <c>HighSeverity</c> or <c>Frequency</c>.</summary>
    public required string Reason { get; init; }

    /// <summary>The severity of the incident that triggered (or contributed to) the stand-down.</summary>
    public int TriggerSeverity { get; init; }

    /// <summary>The number of incidents in the rolling window at the time of the trigger.</summary>
    public int WindowIncidentCount { get; init; }

    /// <summary>When the triggering incident occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>The id of the <see cref="SafetyIncidentReported"/> that triggered this stand-down.</summary>
    public Guid SourceEventId { get; init; }
}
