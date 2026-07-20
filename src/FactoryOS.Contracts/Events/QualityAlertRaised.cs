namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a line-product's rolling defect rate breached its configured threshold. Maintenance,
/// notification, dashboards and AI agents consume it without referencing the Quality module. The rate and unit
/// counts describe the rolling window the breach was observed over, not a single batch.
/// </summary>
public sealed record QualityAlertRaised : IntegrationEvent
{
    /// <summary>The tenant the line belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The production line or workstation the alert is for.</summary>
    public required string LineId { get; init; }

    /// <summary>The product the alert is for.</summary>
    public required string ProductId { get; init; }

    /// <summary>The rolling defect rate that breached the threshold, as a fraction in <c>[0, 1]</c>.</summary>
    public decimal DefectRate { get; init; }

    /// <summary>The configured defect-rate threshold that was exceeded.</summary>
    public decimal Threshold { get; init; }

    /// <summary>Total units inspected across the rolling window.</summary>
    public int WindowInspectedUnits { get; init; }

    /// <summary>Total defective units across the rolling window.</summary>
    public int WindowDefectiveUnits { get; init; }

    /// <summary>When the inspection that triggered the alert took place.</summary>
    public DateTimeOffset InspectedAt { get; init; }

    /// <summary>The id of the <see cref="QualityInspectionRecorded"/> that triggered this alert.</summary>
    public Guid SourceEventId { get; init; }
}
