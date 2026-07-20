using System.Globalization;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Transforms;

/// <summary>
/// Default <see cref="IValueTransformer"/> with a registry of built-in, culture-invariant transforms.
/// An empty or absent transform name is the identity transform, so unmapped values pass through
/// unchanged.
/// </summary>
public sealed class ValueTransformer : IValueTransformer
{
    private readonly Dictionary<string, Func<object?, Result<object?>>> _transforms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["string"] = value => Result.Success<object?>(value?.ToString()),
            ["trim"] = value => Result.Success<object?>(value?.ToString()?.Trim()),
            ["upper"] = value => Result.Success<object?>(value?.ToString()?.ToUpperInvariant()),
            ["lower"] = value => Result.Success<object?>(value?.ToString()?.ToLowerInvariant()),
            ["decimal"] = value => Convert(value, raw => decimal.Parse(raw, CultureInfo.InvariantCulture), "decimal"),
            ["int"] = value => Convert(value, raw => int.Parse(raw, CultureInfo.InvariantCulture), "int"),
            ["bool"] = value => Convert(value, raw => bool.Parse(raw), "bool"),
            ["datetime"] = value => Convert(
                value,
                raw => DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                "datetime"),
        };

    /// <inheritdoc />
    public bool Supports(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _transforms.ContainsKey(name);
    }

    /// <inheritdoc />
    public Result<object?> Apply(string? name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Success(value);
        }

        if (!_transforms.TryGetValue(name, out var transform))
        {
            return Result.Failure<object?>(Error.Validation(
                "Connector.Transform.Unknown",
                $"No value transform named '{name}' is registered."));
        }

        // A null input yields null for every transform; this keeps 'default' handling in the normalizer.
        return value is null ? Result.Success<object?>(null) : transform(value);
    }

    private static Result<object?> Convert(object? value, Func<string, object> parse, string transformName)
    {
        var raw = value?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Success<object?>(null);
        }

        try
        {
            return Result.Success<object?>(parse(raw));
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            return Result.Failure<object?>(Error.Validation(
                "Connector.Transform.Failed",
                $"Value '{raw}' could not be transformed by '{transformName}': {exception.Message}"));
        }
    }
}
