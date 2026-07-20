namespace FactoryOS.Contracts.Iot;

/// <summary>
/// A single raw telemetry reading from a device channel, before normalization. The IoT hub turns it into
/// a Standard Model <see cref="StandardModel.MeterReading"/> using the device's tag calibration.
/// </summary>
/// <param name="DeviceId">The identifier of the device the sample came from.</param>
/// <param name="Tag">The raw channel name the value was read from.</param>
/// <param name="Value">The raw, uncalibrated value.</param>
/// <param name="Timestamp">The instant the sample was captured.</param>
public sealed record TelemetrySample(string DeviceId, string Tag, decimal Value, DateTimeOffset Timestamp);
