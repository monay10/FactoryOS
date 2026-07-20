namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a carbon-equivalent emission was computed from an energy consumption. Reporting,
/// dashboards, sustainability agents and ESG connectors consume it without referencing the Carbon module.
/// </summary>
public sealed record CarbonEmissionCalculated : IntegrationEvent
{
    /// <summary>The tenant the emission belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The source the energy came from (the meter).</summary>
    public required string Source { get; init; }

    /// <summary>The energy metric the emission was derived from (for example <c>ActivePower</c>).</summary>
    public required string Metric { get; init; }

    /// <summary>The energy value the emission was derived from.</summary>
    public decimal EnergyValue { get; init; }

    /// <summary>The unit of the energy value (for example <c>kWh</c>).</summary>
    public required string EnergyUnit { get; init; }

    /// <summary>The emission factor applied, in kg CO₂e per energy unit.</summary>
    public decimal EmissionFactor { get; init; }

    /// <summary>The emission from this reading, in kg CO₂e.</summary>
    public decimal Co2eKg { get; init; }

    /// <summary>The cumulative emission for this source since tracking began, in kg CO₂e.</summary>
    public decimal CumulativeCo2eKg { get; init; }

    /// <summary>When the underlying measurement was taken.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>The id of the <see cref="EnergyConsumptionRecorded"/> the emission was derived from.</summary>
    public Guid SourceEventId { get; init; }
}
