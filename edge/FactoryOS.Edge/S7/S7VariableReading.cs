namespace FactoryOS.Edge.S7;

/// <summary>
/// The raw big-endian bytes read for a single S7 variable, before decoding into telemetry.
/// </summary>
/// <param name="Variable">The variable the bytes were read from.</param>
/// <param name="Data">The raw bytes, in wire (big-endian) order.</param>
/// <param name="SourceTimestamp">The instant the variable was read (the sample timestamp).</param>
public sealed record S7VariableReading(
    S7Variable Variable,
    IReadOnlyList<byte> Data,
    DateTimeOffset SourceTimestamp);
