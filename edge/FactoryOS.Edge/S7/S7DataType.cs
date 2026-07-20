using System.Diagnostics.CodeAnalysis;

namespace FactoryOS.Edge.S7;

/// <summary>How the big-endian bytes behind an S7 variable are interpreted as a number.</summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "Members are named after the canonical Siemens S7 data types (Int, DInt, Word, Real, …).")]
public enum S7DataType
{
    /// <summary>A 16-bit signed integer (<c>INT</c>, 2 bytes).</summary>
    Int,

    /// <summary>A 16-bit unsigned integer (<c>WORD</c>, 2 bytes).</summary>
    Word,

    /// <summary>A 32-bit signed integer (<c>DINT</c>, 4 bytes).</summary>
    DInt,

    /// <summary>A 32-bit unsigned integer (<c>DWORD</c>, 4 bytes).</summary>
    DWord,

    /// <summary>An IEEE-754 single-precision float (<c>REAL</c>, 4 bytes).</summary>
    Real,
}
