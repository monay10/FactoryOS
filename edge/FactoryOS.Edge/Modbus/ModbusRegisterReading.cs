namespace FactoryOS.Edge.Modbus;

/// <summary>
/// The raw 16-bit register words read from a Modbus device at a single address, before decoding into
/// telemetry. A 16-bit value carries one word; a 32-bit value carries two.
/// </summary>
/// <param name="RegisterType">The register space the words were read from.</param>
/// <param name="Address">The starting register address.</param>
/// <param name="Words">The raw register words, in wire (register) order.</param>
public sealed record ModbusRegisterReading(
    ModbusRegisterType RegisterType,
    ushort Address,
    IReadOnlyList<ushort> Words);
