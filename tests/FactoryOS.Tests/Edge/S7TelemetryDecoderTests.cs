using FactoryOS.Edge.S7;

namespace FactoryOS.Tests.Edge;

public sealed class S7TelemetryDecoderTests
{
    private static readonly DateTimeOffset At = new(2026, 07, 20, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Decodes_a_data_block_real()
    {
        var variable = new S7Variable(S7Area.DataBlock, 1, 4, S7DataType.Real);
        var map = new S7VariableMap().Bind(variable, "pm-1", "power");
        var decoder = new S7TelemetryDecoder(map);

        // 25.5f big-endian = 41 CC 00 00.
        var result = decoder.Decode(new S7VariableReading(variable, [0x41, 0xCC, 0x00, 0x00], At));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("pm-1", result.Value.DeviceId);
        Assert.Equal("power", result.Value.Tag);
        Assert.Equal(25.5m, result.Value.Value);
        Assert.Equal(At, result.Value.Timestamp);
    }

    [Fact]
    public void Decodes_a_signed_int()
    {
        var variable = new S7Variable(S7Area.DataBlock, 2, 0, S7DataType.Int);
        var map = new S7VariableMap().Bind(variable, "d", "t");
        var decoder = new S7TelemetryDecoder(map);

        // -2 big-endian INT = FF FE.
        var result = decoder.Decode(new S7VariableReading(variable, [0xFF, 0xFE], At));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(-2m, result.Value.Value);
    }

    [Fact]
    public void Decodes_a_dint()
    {
        var variable = new S7Variable(S7Area.Memory, 0, 20, S7DataType.DInt);
        var map = new S7VariableMap().Bind(variable, "d", "counter");
        var decoder = new S7TelemetryDecoder(map);

        // 100000 big-endian DINT = 00 01 86 A0.
        var result = decoder.Decode(new S7VariableReading(variable, [0x00, 0x01, 0x86, 0xA0], At));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(100000m, result.Value.Value);
    }

    [Fact]
    public void Rejects_an_unmapped_variable()
    {
        var decoder = new S7TelemetryDecoder(new S7VariableMap());
        var variable = new S7Variable(S7Area.DataBlock, 9, 0, S7DataType.Word);

        var result = decoder.Decode(new S7VariableReading(variable, [0x00, 0x01], At));

        Assert.True(result.IsFailure);
        Assert.Equal("Edge.S7.UnmappedVariable", result.Error.Code);
    }

    [Fact]
    public void Rejects_a_reading_with_too_few_bytes()
    {
        var variable = new S7Variable(S7Area.DataBlock, 1, 0, S7DataType.Real);
        var map = new S7VariableMap().Bind(variable, "d", "t");
        var decoder = new S7TelemetryDecoder(map);

        var result = decoder.Decode(new S7VariableReading(variable, [0x41, 0xCC], At));

        Assert.True(result.IsFailure);
        Assert.Equal("Edge.S7.InsufficientData", result.Error.Code);
    }
}
