using FactoryOS.Contracts.Iot;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Domain.Results;
using FactoryOS.Iot.Registry;

namespace FactoryOS.Iot.Telemetry;

/// <summary>
/// Default <see cref="ITelemetryNormalizer"/>. It resolves the device and tag from the registry, applies
/// the tag's linear calibration (<c>value · Scale + Offset</c>) and produces a tenant-scoped meter
/// reading. Unknown or disabled devices, and unknown tags, are rejected rather than silently dropped.
/// </summary>
public sealed class TelemetryNormalizer : ITelemetryNormalizer
{
    private readonly IDeviceRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="TelemetryNormalizer"/> class.</summary>
    /// <param name="registry">The device registry that resolves devices and their tags.</param>
    public TelemetryNormalizer(IDeviceRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public Result<MeterReading> Normalize(TelemetrySample sample, string tenant)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var device = _registry.Find(tenant, sample.DeviceId);
        if (device is null)
        {
            return Result.Failure<MeterReading>(Error.NotFound(
                "Iot.Telemetry.UnknownDevice",
                $"Device '{sample.DeviceId}' is not registered for tenant '{tenant}'."));
        }

        if (!device.Enabled)
        {
            return Result.Failure<MeterReading>(Error.Validation(
                "Iot.Telemetry.DeviceDisabled",
                $"Device '{sample.DeviceId}' is disabled for ingestion."));
        }

        var tag = device.FindTag(sample.Tag);
        if (tag is null)
        {
            return Result.Failure<MeterReading>(Error.NotFound(
                "Iot.Telemetry.UnknownTag",
                $"Device '{sample.DeviceId}' has no tag named '{sample.Tag}'."));
        }

        var value = (sample.Value * tag.Scale) + tag.Offset;

        return Result.Success(new MeterReading
        {
            Tenant = tenant,
            MeterId = sample.DeviceId,
            Metric = tag.Metric,
            Value = value,
            Unit = tag.Unit,
            ReadingAt = sample.Timestamp,
        });
    }
}
