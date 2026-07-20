using FactoryOS.Contracts.Iot;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Domain.Results;

namespace FactoryOS.Iot.Telemetry;

/// <summary>
/// Turns a raw <see cref="TelemetrySample"/> into a Standard Model <see cref="MeterReading"/> using the
/// registered device's tag calibration. This is where PLC/IoT dialects become the Standard Model.
/// </summary>
public interface ITelemetryNormalizer
{
    /// <summary>Normalizes a single telemetry sample for a tenant.</summary>
    /// <param name="sample">The raw sample.</param>
    /// <param name="tenant">The tenant the sample belongs to.</param>
    /// <returns>A successful result with the meter reading, or a failure when the device or tag is unknown.</returns>
    Result<MeterReading> Normalize(TelemetrySample sample, string tenant);
}
