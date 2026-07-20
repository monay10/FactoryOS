namespace FactoryOS.Edge.S7;

/// <summary>
/// The address of a Siemens S7 variable: an area, an optional data-block number, a byte offset and the data
/// type stored there (for example <c>DB1.DBD4</c> as <see cref="S7Area.DataBlock"/> / DB 1 / byte 4 / REAL).
/// </summary>
/// <param name="Area">The memory area.</param>
/// <param name="DbNumber">The data-block number; <c>0</c> for non-<see cref="S7Area.DataBlock"/> areas.</param>
/// <param name="ByteOffset">The byte offset within the area or block.</param>
/// <param name="DataType">How the bytes at the offset are interpreted.</param>
public sealed record S7Variable(S7Area Area, int DbNumber, int ByteOffset, S7DataType DataType);
