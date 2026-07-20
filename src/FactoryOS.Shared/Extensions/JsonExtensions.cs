using System.Text.Json;

namespace FactoryOS.Shared.Extensions;

/// <summary>
/// JSON serialization helpers over <see cref="System.Text.Json"/>, using a single cached, web-default options
/// instance (camelCase, case-insensitive) so callers get consistent output without allocating options per call.
/// </summary>
public static class JsonExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    /// <summary>Serializes a value to a compact JSON string.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The JSON representation.</returns>
    public static string ToJson<T>(this T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Deserializes a JSON string to a value.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="json">The JSON to parse.</param>
    /// <returns>The deserialized value, or <see langword="null"/> when the JSON is a literal null.</returns>
    public static T? FromJson<T>(this string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<T>(json, Options);
    }
}
