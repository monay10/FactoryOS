using FactoryOS.Contracts.Iot;
using FactoryOS.Iot.Registry;
using FactoryOS.Iot.Telemetry;

namespace FactoryOS.Tests.Iot;

public sealed class TelemetryNormalizerTests
{
    private static readonly DateTimeOffset At = new(2026, 07, 20, 09, 00, 00, TimeSpan.Zero);

    private readonly IDeviceRegistry _registry = new InMemoryDeviceRegistry();
    private readonly ITelemetryNormalizer _normalizer;

    public TelemetryNormalizerTests() => _normalizer = new TelemetryNormalizer(_registry);

    private void RegisterPowerMeter(bool enabled = true) => _registry.Register(new Device
    {
        Tenant = "acme",
        DeviceId = "pm-1",
        Name = "Power Meter 1",
        Enabled = enabled,
        Tags = [new DeviceTag { Name = "ch1", Metric = "ActivePower", Unit = "kW", Scale = 0.1m, Offset = 2m }],
    });

    [Fact]
    public void Applies_the_tag_calibration_and_maps_to_a_meter_reading()
    {
        RegisterPowerMeter();

        var result = _normalizer.Normalize(new TelemetrySample("pm-1", "ch1", 100m, At), "acme");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("acme", result.Value.Tenant);
        Assert.Equal("pm-1", result.Value.MeterId);
        Assert.Equal("ActivePower", result.Value.Metric);
        Assert.Equal(12m, result.Value.Value); // 100 * 0.1 + 2
        Assert.Equal("kW", result.Value.Unit);
        Assert.Equal(At, result.Value.ReadingAt);
    }

    [Fact]
    public void Rejects_an_unregistered_device()
    {
        var result = _normalizer.Normalize(new TelemetrySample("ghost", "ch1", 1m, At), "acme");

        Assert.True(result.IsFailure);
        Assert.Equal("Iot.Telemetry.UnknownDevice", result.Error.Code);
    }

    [Fact]
    public void Rejects_a_disabled_device()
    {
        RegisterPowerMeter(enabled: false);

        var result = _normalizer.Normalize(new TelemetrySample("pm-1", "ch1", 1m, At), "acme");

        Assert.True(result.IsFailure);
        Assert.Equal("Iot.Telemetry.DeviceDisabled", result.Error.Code);
    }

    [Fact]
    public void Rejects_an_unknown_tag()
    {
        RegisterPowerMeter();

        var result = _normalizer.Normalize(new TelemetrySample("pm-1", "ch9", 1m, At), "acme");

        Assert.True(result.IsFailure);
        Assert.Equal("Iot.Telemetry.UnknownTag", result.Error.Code);
    }
}
