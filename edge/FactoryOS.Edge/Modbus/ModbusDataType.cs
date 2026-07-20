using System.Diagnostics.CodeAnalysis;

namespace FactoryOS.Edge.Modbus;

/// <summary>How the 16-bit register words behind an address are interpreted as a number.</summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "Members are named after the canonical Modbus wire data types (Int16, UInt32, Float32, …).")]
public enum ModbusDataType
{
    /// <summary>A single register read as a signed 16-bit integer.</summary>
    Int16,

    /// <summary>A single register read as an unsigned 16-bit integer.</summary>
    UInt16,

    /// <summary>Two registers read as a signed 32-bit integer.</summary>
    Int32,

    /// <summary>Two registers read as an unsigned 32-bit integer.</summary>
    UInt32,

    /// <summary>Two registers read as an IEEE-754 single-precision float.</summary>
    Float32,
}

/// <summary>The order of the two 16-bit words that make up a 32-bit value.</summary>
public enum ModbusWordOrder
{
    /// <summary>Most-significant word first (Modbus/big-endian, the specification default).</summary>
    BigEndian,

    /// <summary>Least-significant word first (the common "word-swapped" layout on many PLCs).</summary>
    LittleEndian,
}
