using FactoryOS.Contracts.Iot;
using FactoryOS.Edge.Modbus;
using FactoryOS.Edge.S7;
using FactoryOS.Iot.Registry;
using FactoryOS.Iot.Telemetry;

namespace FactoryOS.IntegrationTests.Edge;

/// <summary>
/// Proves the fieldbus edge → IoT hub path: Modbus register reads and Siemens S7 variable reads decode to
/// raw telemetry samples, which the hub normalizes into Standard Model meter readings using device
/// calibration — exactly like the MQTT/OPC-UA path, over a different set of drivers.
/// </summary>
public sealed class FieldbusToHubTests
{
    private static readonly DateTimeOffset At = new(2026, 07, 20, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Modbus_and_s7_reads_normalize_into_calibrated_meter_readings()
    {
        var registry = new InMemoryDeviceRegistry();
        registry.Register(new Device
        {
            Tenant = "acme",
            DeviceId = "line-1",
            Name = "Line 1 Controller",
            Tags =
            [
                // Modbus reports energy in Wh; the hub scales to kWh.
                new DeviceTag { Name = "energy", Metric = "ActiveEnergy", Unit = "kWh", Scale = 0.001m },
                // S7 reports temperature in °C already.
                new DeviceTag { Name = "temp", Metric = "Temperature", Unit = "C" },
            ],
        });

        var ingestor = new TelemetryIngestor(new TelemetryNormalizer(registry));

        var modbus = new ModbusTelemetryDecoder(new ModbusRegisterMap()
            .Bind(ModbusRegisterType.InputRegister, 100, ModbusDataType.UInt32, "line-1", "energy"));
        var s7Var = new S7Variable(S7Area.DataBlock, 1, 4, S7DataType.Real);
        var s7 = new S7TelemetryDecoder(new S7VariableMap().Bind(s7Var, "line-1", "temp"));

        var samples = new List<TelemetrySample>
        {
            // 0x0001_86A0 = 100000 Wh.
            modbus.Decode(new ModbusRegisterReading(ModbusRegisterType.InputRegister, 100, [0x0001, 0x86A0]), At).Value,
            // 25.5f big-endian = 41 CC 00 00.
            s7.Decode(new S7VariableReading(s7Var, [0x41, 0xCC, 0x00, 0x00], At)).Value,
        };

        var result = ingestor.Ingest(samples, "acme");

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Readings.Count);

        var energy = result.Readings.Single(r => r.Metric == "ActiveEnergy");
        Assert.Equal(100m, energy.Value); // 100000 Wh * 0.001 = 100 kWh

        var temperature = result.Readings.Single(r => r.Metric == "Temperature");
        Assert.Equal(25.5m, temperature.Value);
    }
}
