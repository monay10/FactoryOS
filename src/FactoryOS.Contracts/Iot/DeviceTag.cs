namespace FactoryOS.Contracts.Iot;

/// <summary>
/// One channel a device exposes, and how it maps to the Standard Model. A tag names the raw source
/// channel and the canonical metric, unit and linear calibration (<c>value · Scale + Offset</c>) used to
/// turn a raw sample into a <see cref="StandardModel.MeterReading"/>.
/// </summary>
public sealed record DeviceTag
{
    /// <summary>Gets the raw source channel name as reported by the device (for example <c>ch1</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the canonical metric the tag measures (for example <c>ActivePower</c>).</summary>
    public required string Metric { get; init; }

    /// <summary>Gets the unit of the calibrated value (for example <c>kWh</c> or <c>°C</c>).</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>Gets the multiplicative calibration factor applied to the raw value; defaults to 1.</summary>
    public decimal Scale { get; init; } = 1m;

    /// <summary>Gets the additive calibration offset applied after scaling; defaults to 0.</summary>
    public decimal Offset { get; init; }
}
