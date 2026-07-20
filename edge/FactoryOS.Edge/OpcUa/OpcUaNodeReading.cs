namespace FactoryOS.Edge.OpcUa;

/// <summary>A value change read from an OPC-UA node, before decoding into telemetry.</summary>
/// <param name="NodeId">The OPC-UA node identifier (for example <c>ns=2;s=Line1.Power</c>).</param>
/// <param name="Value">The node value.</param>
/// <param name="SourceTimestamp">The server-reported source timestamp of the value.</param>
public sealed record OpcUaNodeReading(string NodeId, decimal Value, DateTimeOffset SourceTimestamp);
