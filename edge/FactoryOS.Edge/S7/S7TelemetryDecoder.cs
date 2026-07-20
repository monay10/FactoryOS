using System.Buffers.Binary;
using FactoryOS.Contracts.Iot;
using FactoryOS.Domain.Results;

namespace FactoryOS.Edge.S7;

/// <summary>
/// Decodes an <see cref="S7VariableReading"/> into a raw <see cref="TelemetrySample"/> by resolving its
/// address through an <see cref="S7VariableMap"/> and interpreting the big-endian bytes per the variable's
/// data type. The edge never calibrates or normalizes — that is the IoT hub's job.
/// </summary>
public sealed class S7TelemetryDecoder
{
    private readonly S7VariableMap _map;

    /// <summary>Initializes a new instance of the <see cref="S7TelemetryDecoder"/> class.</summary>
    /// <param name="map">The variable map that resolves addresses to devices and tags.</param>
    public S7TelemetryDecoder(S7VariableMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
    }

    /// <summary>Decodes a variable reading into a telemetry sample.</summary>
    /// <param name="reading">The raw variable reading.</param>
    /// <returns>A successful result with the sample, or a failure when the variable is unmapped or the bytes are insufficient.</returns>
    public Result<TelemetrySample> Decode(S7VariableReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (!_map.TryResolve(reading.Variable, out var deviceId, out var tag))
        {
            return Result.Failure<TelemetrySample>(Error.NotFound(
                "Edge.S7.UnmappedVariable",
                $"S7 variable {Describe(reading.Variable)} is not bound to any device tag."));
        }

        var required = ByteWidth(reading.Variable.DataType);
        if (reading.Data is null || reading.Data.Count < required)
        {
            return Result.Failure<TelemetrySample>(Error.Validation(
                "Edge.S7.InsufficientData",
                $"Data type {reading.Variable.DataType} needs {required} byte(s) but the reading carried {reading.Data?.Count ?? 0}."));
        }

        var value = Interpret(reading.Variable.DataType, reading.Data);
        return Result.Success(new TelemetrySample(deviceId, tag, value, reading.SourceTimestamp));
    }

    private static int ByteWidth(S7DataType dataType) => dataType switch
    {
        S7DataType.Int or S7DataType.Word => 2,
        _ => 4,
    };

    private static decimal Interpret(S7DataType dataType, IReadOnlyList<byte> data)
    {
        Span<byte> bytes = stackalloc byte[ByteWidth(dataType)];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = data[i];
        }

        return dataType switch
        {
            S7DataType.Int => BinaryPrimitives.ReadInt16BigEndian(bytes),
            S7DataType.Word => BinaryPrimitives.ReadUInt16BigEndian(bytes),
            S7DataType.DInt => BinaryPrimitives.ReadInt32BigEndian(bytes),
            S7DataType.DWord => BinaryPrimitives.ReadUInt32BigEndian(bytes),
            _ => (decimal)BinaryPrimitives.ReadSingleBigEndian(bytes),
        };
    }

    private static string Describe(S7Variable variable) => variable.Area == S7Area.DataBlock
        ? $"DB{variable.DbNumber}.{variable.ByteOffset} ({variable.DataType})"
        : $"{variable.Area}.{variable.ByteOffset} ({variable.DataType})";
}
