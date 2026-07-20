using FactoryOS.Contracts.Iot;
using FactoryOS.Domain.Results;

namespace FactoryOS.Edge.OpcUa;

/// <summary>
/// Decodes an <see cref="OpcUaNodeReading"/> into a raw <see cref="TelemetrySample"/> by resolving its
/// node id through an <see cref="OpcUaNodeMap"/>. The value and timestamp pass through unchanged;
/// calibration is the IoT hub's job.
/// </summary>
public sealed class OpcUaTelemetryDecoder
{
    private readonly OpcUaNodeMap _map;

    /// <summary>Initializes a new instance of the <see cref="OpcUaTelemetryDecoder"/> class.</summary>
    /// <param name="map">The node map that resolves node ids to devices and tags.</param>
    public OpcUaTelemetryDecoder(OpcUaNodeMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
    }

    /// <summary>Decodes a node reading into a telemetry sample.</summary>
    /// <param name="reading">The OPC-UA node reading.</param>
    /// <returns>A successful result with the sample, or a failure when the node id is not bound.</returns>
    public Result<TelemetrySample> Decode(OpcUaNodeReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (!_map.TryResolve(reading.NodeId, out var deviceId, out var tag))
        {
            return Result.Failure<TelemetrySample>(Error.NotFound(
                "Edge.OpcUa.UnmappedNode",
                $"OPC-UA node '{reading.NodeId}' is not bound to any device tag."));
        }

        return Result.Success(new TelemetrySample(deviceId, tag, reading.Value, reading.SourceTimestamp));
    }
}
