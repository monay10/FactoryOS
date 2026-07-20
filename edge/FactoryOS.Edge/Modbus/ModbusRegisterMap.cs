namespace FactoryOS.Edge.Modbus;

/// <summary>
/// Maps Modbus register addresses to a device tag and the data type used to interpret them. This is the
/// mapping-as-data that lets the edge turn opaque register banks into Standard-Model-bound telemetry
/// without any register-specific code.
/// </summary>
public sealed class ModbusRegisterMap
{
    private readonly Dictionary<(ModbusRegisterType Type, ushort Address), ModbusRegisterBinding> _bindings = [];

    /// <summary>Binds a register address to a device tag.</summary>
    /// <param name="registerType">The register space the address lives in.</param>
    /// <param name="address">The starting register address.</param>
    /// <param name="dataType">How the register words are interpreted as a number.</param>
    /// <param name="deviceId">The device the register belongs to.</param>
    /// <param name="tag">The device tag the register feeds.</param>
    /// <param name="wordOrder">The word order for 32-bit values; ignored for 16-bit values.</param>
    /// <returns>The same <see cref="ModbusRegisterMap"/> instance, to allow chaining.</returns>
    public ModbusRegisterMap Bind(
        ModbusRegisterType registerType,
        ushort address,
        ModbusDataType dataType,
        string deviceId,
        string tag,
        ModbusWordOrder wordOrder = ModbusWordOrder.BigEndian)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        _bindings[(registerType, address)] = new ModbusRegisterBinding(dataType, deviceId, tag, wordOrder);
        return this;
    }

    /// <summary>Resolves a register address to its binding.</summary>
    /// <param name="registerType">The register space.</param>
    /// <param name="address">The starting register address.</param>
    /// <param name="binding">The resolved binding when found.</param>
    /// <returns><see langword="true"/> when the address is bound; otherwise <see langword="false"/>.</returns>
    public bool TryResolve(ModbusRegisterType registerType, ushort address, out ModbusRegisterBinding binding)
        => _bindings.TryGetValue((registerType, address), out binding!);
}

/// <summary>A single Modbus register binding: how to read an address and where its value belongs.</summary>
/// <param name="DataType">How the register words are interpreted as a number.</param>
/// <param name="DeviceId">The device the register belongs to.</param>
/// <param name="Tag">The device tag the register feeds.</param>
/// <param name="WordOrder">The word order for 32-bit values.</param>
public sealed record ModbusRegisterBinding(
    ModbusDataType DataType,
    string DeviceId,
    string Tag,
    ModbusWordOrder WordOrder);
