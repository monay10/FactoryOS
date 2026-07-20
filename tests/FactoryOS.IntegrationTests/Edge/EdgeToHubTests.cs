using FactoryOS.Contracts.Iot;
using FactoryOS.Edge.Mqtt;
using FactoryOS.Edge.OpcUa;
using FactoryOS.Iot.Registry;
using FactoryOS.Iot.Telemetry;

namespace FactoryOS.IntegrationTests.Edge;

/// <summary>
/// Proves the full edge → IoT hub path: protocol frames decode to raw telemetry samples, which the hub
/// normalizes into Standard Model meter readings using device calibration.
/// </summary>
public sealed class EdgeToHubTests
{
    private static readonly DateTimeOffset At = new(2026, 07, 20, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Mqtt_and_opcua_frames_normalize_into_calibrated_meter_readings()
    {
        var registry = new InMemoryDeviceRegistry();
        registry.Register(new Device
        {
            Tenant = "acme",
            DeviceId = "pm-1",
            Name = "Power Meter 1",
            Tags =
            [
                new DeviceTag { Name = "power", Metric = "ActivePower", Unit = "kW", Scale = 0.001m },
                new DeviceTag { Name = "temp", Metric = "Temperature", Unit = "C", Offset = -273.15m },
            ],
        });

        var ingestor = new TelemetryIngestor(new TelemetryNormalizer(registry));

        var mqtt = new MqttTelemetryDecoder(new MqttTopicTemplate("factory/{device}/{tag}"));
        var opcua = new OpcUaTelemetryDecoder(new OpcUaNodeMap().Bind("ns=2;s=PM1.Temp", "pm-1", "temp"));

        var samples = new List<TelemetrySample>
        {
            mqtt.Decode(new MqttMessage("factory/pm-1/power", "5000"), At).Value,
            opcua.Decode(new OpcUaNodeReading("ns=2;s=PM1.Temp", 298.15m, At)).Value,
        };

        var result = ingestor.Ingest(samples, "acme");

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Readings.Count);

        var power = result.Readings.Single(r => r.Metric == "ActivePower");
        Assert.Equal(5m, power.Value); // 5000 * 0.001 kW

        var temperature = result.Readings.Single(r => r.Metric == "Temperature");
        Assert.Equal(25m, temperature.Value); // 298.15 K − 273.15
    }
}
