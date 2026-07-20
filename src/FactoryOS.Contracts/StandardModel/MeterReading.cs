namespace FactoryOS.Contracts.StandardModel;

/// <summary>
/// Canonical representation of a single measurement from a meter or sensor (for example an energy or
/// temperature reading). PLC tags and IoT telemetry normalize into this entity.
/// </summary>
public sealed record MeterReading : IStandardEntity
{
    /// <summary>The canonical entity type name.</summary>
    public const string Type = "MeterReading";

    /// <inheritdoc />
    public required string Tenant { get; init; }

    /// <summary>Gets the identifier of the meter or sensor the reading came from.</summary>
    public required string MeterId { get; init; }

    /// <summary>Gets the measured metric (for example <c>ActivePower</c> or <c>Temperature</c>).</summary>
    public required string Metric { get; init; }

    /// <summary>Gets the measured value.</summary>
    public decimal Value { get; init; }

    /// <summary>Gets the unit of measure of the value (for example <c>kWh</c> or <c>°C</c>).</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>Gets the instant the measurement was taken.</summary>
    public DateTimeOffset ReadingAt { get; init; }

    /// <inheritdoc />
    public string EntityType => Type;

    /// <inheritdoc />
    public string NaturalKey => $"{MeterId}:{Metric}:{ReadingAt.UtcTicks}";
}
