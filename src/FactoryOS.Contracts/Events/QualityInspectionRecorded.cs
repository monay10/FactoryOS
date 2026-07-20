namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a batch of units was inspected on a line for a product: how many were checked and how
/// many were defective. Any module consumes it without referencing the producer; the Quality module folds it
/// into a rolling defect rate and may answer with a <see cref="QualityAlertRaised"/>.
/// </summary>
public sealed record QualityInspectionRecorded : IntegrationEvent
{
    /// <summary>The tenant the line belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The production line or workstation the inspection was performed on.</summary>
    public required string LineId { get; init; }

    /// <summary>The product being inspected.</summary>
    public required string ProductId { get; init; }

    /// <summary>How many units were inspected in this batch.</summary>
    public int InspectedUnits { get; init; }

    /// <summary>How many of the inspected units were defective (out of spec).</summary>
    public int DefectiveUnits { get; init; }

    /// <summary>When the inspection took place.</summary>
    public DateTimeOffset InspectedAt { get; init; }
}
