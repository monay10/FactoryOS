using FactoryOS.Edge.Modbus;

namespace FactoryOS.Tests.Edge;

public sealed class ModbusTelemetryDecoderTests
{
    private static readonly DateTimeOffset At = new(2026, 07, 20, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Decodes_a_signed_16_bit_holding_register()
    {
        var map = new ModbusRegisterMap()
            .Bind(ModbusRegisterType.HoldingRegister, 40, ModbusDataType.Int16, "pm-1", "power");
        var decoder = new ModbusTelemetryDecoder(map);

        var result = decoder.Decode(
            new ModbusRegisterReading(ModbusRegisterType.HoldingRegister, 40, [unchecked((ushort)-1234)]),
            At);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("pm-1", result.Value.DeviceId);
        Assert.Equal("power", result.Value.Tag);
        Assert.Equal(-1234m, result.Value.Value);
        Assert.Equal(At, result.Value.Timestamp);
    }

    [Fact]
    public void Decodes_a_big_endian_32_bit_unsigned_integer_across_two_registers()
    {
        var map = new ModbusRegisterMap()
            .Bind(ModbusRegisterType.InputRegister, 100, ModbusDataType.UInt32, "em-1", "energy");
        var decoder = new ModbusTelemetryDecoder(map);

        // 0x0001_86A0 = 100000; high word first.
        var result = decoder.Decode(
            new ModbusRegisterReading(ModbusRegisterType.InputRegister, 100, [0x0001, 0x86A0]),
            At);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(100000m, result.Value.Value);
    }

    [Fact]
    public void Honours_little_endian_word_order()
    {
        var map = new ModbusRegisterMap()
            .Bind(ModbusRegisterType.HoldingRegister, 10, ModbusDataType.UInt32, "d", "t", ModbusWordOrder.LittleEndian);
        var decoder = new ModbusTelemetryDecoder(map);

        // Words swapped: low word first.
        var result = decoder.Decode(
            new ModbusRegisterReading(ModbusRegisterType.HoldingRegister, 10, [0x86A0, 0x0001]),
            At);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(100000m, result.Value.Value);
    }

    [Fact]
    public void Decodes_a_float32_register_pair()
    {
        var map = new ModbusRegisterMap()
            .Bind(ModbusRegisterType.HoldingRegister, 200, ModbusDataType.Float32, "tm-1", "temp");
        var decoder = new ModbusTelemetryDecoder(map);

        // 25.5f = 0x41CC0000; high word first.
        var result = decoder.Decode(
            new ModbusRegisterReading(ModbusRegisterType.HoldingRegister, 200, [0x41CC, 0x0000]),
            At);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(25.5m, result.Value.Value);
    }

    [Fact]
    public void Rejects_an_unmapped_address()
    {
        var decoder = new ModbusTelemetryDecoder(new ModbusRegisterMap());

        var result = decoder.Decode(
            new ModbusRegisterReading(ModbusRegisterType.HoldingRegister, 999, [0]),
            At);

        Assert.True(result.IsFailure);
        Assert.Equal("Edge.Modbus.UnmappedRegister", result.Error.Code);
    }

    [Fact]
    public void Rejects_a_reading_with_too_few_words_for_the_data_type()
    {
        var map = new ModbusRegisterMap()
            .Bind(ModbusRegisterType.HoldingRegister, 10, ModbusDataType.Int32, "d", "t");
        var decoder = new ModbusTelemetryDecoder(map);

        var result = decoder.Decode(
            new ModbusRegisterReading(ModbusRegisterType.HoldingRegister, 10, [0x0001]),
            At);

        Assert.True(result.IsFailure);
        Assert.Equal("Edge.Modbus.InsufficientRegisters", result.Error.Code);
    }
}
