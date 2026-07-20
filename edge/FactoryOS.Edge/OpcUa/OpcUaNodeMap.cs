namespace FactoryOS.Edge.OpcUa;

/// <summary>
/// Maps OPC-UA node identifiers to a device and tag. This is the mapping-as-data that lets the edge turn
/// opaque node ids into Standard-Model-bound telemetry without any node-specific code.
/// </summary>
public sealed class OpcUaNodeMap
{
    private readonly Dictionary<string, (string DeviceId, string Tag)> _bindings = new(StringComparer.Ordinal);

    /// <summary>Binds a node id to a device and tag.</summary>
    /// <param name="nodeId">The OPC-UA node identifier.</param>
    /// <param name="deviceId">The device the node belongs to.</param>
    /// <param name="tag">The device tag the node feeds.</param>
    /// <returns>The same <see cref="OpcUaNodeMap"/> instance, to allow chaining.</returns>
    public OpcUaNodeMap Bind(string nodeId, string deviceId, string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        _bindings[nodeId] = (deviceId, tag);
        return this;
    }

    /// <summary>Resolves a node id to its device and tag.</summary>
    /// <param name="nodeId">The OPC-UA node identifier.</param>
    /// <param name="deviceId">The bound device id when resolved.</param>
    /// <param name="tag">The bound tag when resolved.</param>
    /// <returns><see langword="true"/> when the node is bound; otherwise <see langword="false"/>.</returns>
    public bool TryResolve(string nodeId, out string deviceId, out string tag)
    {
        if (nodeId is not null && _bindings.TryGetValue(nodeId, out var binding))
        {
            (deviceId, tag) = binding;
            return true;
        }

        deviceId = string.Empty;
        tag = string.Empty;
        return false;
    }
}
