namespace FactoryOS.Contracts.Events;

/// <summary>
/// Emitted by the Energy module for every meter reading it records against its rolling baseline. It is shared
/// vocabulary on the bus so any module (History, OEE, reporting) can consume it without referencing Energy.
/// </summary>
public sealed record EnergyConsumptionRecorded : IntegrationEvent
{
    /// <summary>The tenant the reading belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The meter the reading came from.</summary>
    public required string MeterId { get; init; }

    /// <summary>The measured metric (for example <c>ActivePower</c>).</summary>
    public required string Metric { get; init; }

    /// <summary>The recorded value.</summary>
    public decimal Value { get; init; }

    /// <summary>The unit of measure (for example <c>kWh</c>).</summary>
    public required string Unit { get; init; }

    /// <summary>The instant the measurement was taken.</summary>
    public DateTimeOffset ReadingAt { get; init; }
}
