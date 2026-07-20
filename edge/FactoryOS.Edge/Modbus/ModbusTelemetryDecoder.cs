using FactoryOS.Contracts.Iot;
using FactoryOS.Domain.Results;

namespace FactoryOS.Edge.Modbus;

/// <summary>
/// Decodes a <see cref="ModbusRegisterReading"/> into a raw <see cref="TelemetrySample"/> by resolving its
/// address through a <see cref="ModbusRegisterMap"/> and interpreting the register words per the bound data
/// type. The edge never calibrates or normalizes — that is the IoT hub's job.
/// </summary>
public sealed class ModbusTelemetryDecoder
{
    private readonly ModbusRegisterMap _map;

    /// <summary>Initializes a new instance of the <see cref="ModbusTelemetryDecoder"/> class.</summary>
    /// <param name="map">The register map that resolves addresses to devices and tags.</param>
    public ModbusTelemetryDecoder(ModbusRegisterMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
    }

    /// <summary>Decodes a register reading captured at <paramref name="readAt"/> into a telemetry sample.</summary>
    /// <param name="reading">The raw register reading.</param>
    /// <param name="readAt">The instant the registers were polled (the sample timestamp).</param>
    /// <returns>A successful result with the sample, or a failure when the address is unmapped or the words are insufficient.</returns>
    public Result<TelemetrySample> Decode(ModbusRegisterReading reading, DateTimeOffset readAt)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (!_map.TryResolve(reading.RegisterType, reading.Address, out var binding))
        {
            return Result.Failure<TelemetrySample>(Error.NotFound(
                "Edge.Modbus.UnmappedRegister",
                $"Modbus {reading.RegisterType} address {reading.Address} is not bound to any device tag."));
        }

        var required = WordCount(binding.DataType);
        if (reading.Words is null || reading.Words.Count < required)
        {
            return Result.Failure<TelemetrySample>(Error.Validation(
                "Edge.Modbus.InsufficientRegisters",
                $"Data type {binding.DataType} needs {required} register word(s) but the reading carried {reading.Words?.Count ?? 0}."));
        }

        var value = Interpret(binding, reading.Words);
        return Result.Success(new TelemetrySample(binding.DeviceId, binding.Tag, value, readAt));
    }

    private static int WordCount(ModbusDataType dataType) => dataType switch
    {
        ModbusDataType.Int16 or ModbusDataType.UInt16 => 1,
        _ => 2,
    };

    private static decimal Interpret(ModbusRegisterBinding binding, IReadOnlyList<ushort> words)
    {
        switch (binding.DataType)
        {
            case ModbusDataType.Int16:
                return unchecked((short)words[0]);
            case ModbusDataType.UInt16:
                return words[0];
            default:
                break;
        }

        // 32-bit: assemble the double word honouring the configured word order.
        var high = binding.WordOrder == ModbusWordOrder.BigEndian ? words[0] : words[1];
        var low = binding.WordOrder == ModbusWordOrder.BigEndian ? words[1] : words[0];
        var raw = ((uint)high << 16) | low;

        return binding.DataType switch
        {
            ModbusDataType.Int32 => unchecked((int)raw),
            ModbusDataType.UInt32 => raw,
            _ => (decimal)BitConverter.UInt32BitsToSingle(raw),
        };
    }
}
