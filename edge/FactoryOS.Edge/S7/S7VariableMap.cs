namespace FactoryOS.Edge.S7;

/// <summary>
/// Maps Siemens S7 variable addresses to a device and tag. This is the mapping-as-data that lets the edge
/// turn PLC memory locations into Standard-Model-bound telemetry without any address-specific code.
/// </summary>
public sealed class S7VariableMap
{
    private readonly Dictionary<(S7Area Area, int DbNumber, int ByteOffset), (string DeviceId, string Tag)> _bindings = [];

    /// <summary>Binds an S7 variable to a device and tag.</summary>
    /// <param name="variable">The variable address. Only area, block and byte offset identify it; the data type is not part of the key.</param>
    /// <param name="deviceId">The device the variable belongs to.</param>
    /// <param name="tag">The device tag the variable feeds.</param>
    /// <returns>The same <see cref="S7VariableMap"/> instance, to allow chaining.</returns>
    public S7VariableMap Bind(S7Variable variable, string deviceId, string tag)
    {
        ArgumentNullException.ThrowIfNull(variable);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        _bindings[(variable.Area, variable.DbNumber, variable.ByteOffset)] = (deviceId, tag);
        return this;
    }

    /// <summary>Resolves a variable to its device and tag.</summary>
    /// <param name="variable">The variable address.</param>
    /// <param name="deviceId">The bound device id when resolved.</param>
    /// <param name="tag">The bound tag when resolved.</param>
    /// <returns><see langword="true"/> when the variable is bound; otherwise <see langword="false"/>.</returns>
    public bool TryResolve(S7Variable variable, out string deviceId, out string tag)
    {
        if (variable is not null &&
            _bindings.TryGetValue((variable.Area, variable.DbNumber, variable.ByteOffset), out var binding))
        {
            (deviceId, tag) = binding;
            return true;
        }

        deviceId = string.Empty;
        tag = string.Empty;
        return false;
    }
}
