using FactoryOS.Edge.OpcUa;

namespace FactoryOS.Tests.Edge;

public sealed class OpcUaTelemetryDecoderTests
{
    private static readonly DateTimeOffset At = new(2026, 07, 20, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Resolves_a_bound_node_to_a_telemetry_sample()
    {
        var map = new OpcUaNodeMap().Bind("ns=2;s=Line1.Power", "pm-1", "power");
        var decoder = new OpcUaTelemetryDecoder(map);

        var result = decoder.Decode(new OpcUaNodeReading("ns=2;s=Line1.Power", 42.5m, At));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("pm-1", result.Value.DeviceId);
        Assert.Equal("power", result.Value.Tag);
        Assert.Equal(42.5m, result.Value.Value);
        Assert.Equal(At, result.Value.Timestamp);
    }

    [Fact]
    public void Rejects_an_unmapped_node()
    {
        var decoder = new OpcUaTelemetryDecoder(new OpcUaNodeMap());

        var result = decoder.Decode(new OpcUaNodeReading("ns=2;s=Unknown", 1m, At));

        Assert.True(result.IsFailure);
        Assert.Equal("Edge.OpcUa.UnmappedNode", result.Error.Code);
    }
}
