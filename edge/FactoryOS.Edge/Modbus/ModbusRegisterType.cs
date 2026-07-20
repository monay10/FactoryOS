namespace FactoryOS.Edge.Modbus;

/// <summary>The Modbus register space a reading comes from.</summary>
public enum ModbusRegisterType
{
    /// <summary>Read/write 16-bit registers (function codes 3/6/16).</summary>
    HoldingRegister,

    /// <summary>Read-only 16-bit registers (function code 4).</summary>
    InputRegister,
}
