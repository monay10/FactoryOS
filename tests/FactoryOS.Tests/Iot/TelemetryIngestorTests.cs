using FactoryOS.Contracts.Iot;
using FactoryOS.Iot.Registry;
using FactoryOS.Iot.Telemetry;

namespace FactoryOS.Tests.Iot;

public sealed class TelemetryIngestorTests
{
    private static readonly DateTimeOffset At = new(2026, 07, 20, 09, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Normalizes_good_samples_and_collects_errors_for_the_rest()
    {
        var registry = new InMemoryDeviceRegistry();
        registry.Register(new Device
        {
            Tenant = "acme",
            DeviceId = "pm-1",
            Name = "Power Meter 1",
            Tags = [new DeviceTag { Name = "ch1", Metric = "ActivePower", Unit = "kW" }],
        });

        var ingestor = new TelemetryIngestor(new TelemetryNormalizer(registry));

        var result = ingestor.Ingest(
        [
            new TelemetrySample("pm-1", "ch1", 5m, At),
            new TelemetrySample("pm-1", "ch9", 7m, At),   // unknown tag
            new TelemetrySample("ghost", "ch1", 9m, At),  // unknown device
        ],
        "acme");

        Assert.Equal(3, result.Read);
        Assert.Single(result.Readings);
        Assert.Equal(5m, result.Readings[0].Value);
        Assert.Equal(2, result.Errors.Count);
    }
}
