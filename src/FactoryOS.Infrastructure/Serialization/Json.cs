using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryOS.Shared.Guards;

namespace FactoryOS.Infrastructure.Serialization;

/// <summary>Serializes and deserializes objects, abstracted so callers do not bind to a concrete serializer.</summary>
public interface IJsonSerializer
{
    /// <summary>Serializes a value to a JSON string.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The JSON representation.</returns>
    string Serialize<T>(T value);

    /// <summary>Deserializes a JSON string to a value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="json">The JSON to deserialize.</param>
    /// <returns>The deserialized value, or <see langword="null"/> when the JSON represents <c>null</c>.</returns>
    T? Deserialize<T>(string json);

    /// <summary>Serializes a value to a UTF-8 byte array.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The UTF-8 encoded JSON bytes.</returns>
    byte[] SerializeToUtf8Bytes<T>(T value);

    /// <summary>Deserializes UTF-8 encoded JSON bytes to a value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="utf8Json">The UTF-8 encoded JSON.</param>
    /// <returns>The deserialized value, or <see langword="null"/> when the JSON represents <c>null</c>.</returns>
    T? Deserialize<T>(ReadOnlySpan<byte> utf8Json);
}

/// <summary>The single, canonical <see cref="JsonSerializerOptions"/> used across the platform.</summary>
public static class JsonOptions
{
    /// <summary>
    /// Gets the default options: camel-cased property names, case-insensitive reads, enums as strings, no
    /// indentation and skipped <c>null</c> values — the wire format every infrastructure component shares.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = CreateDefault();

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>The default <see cref="IJsonSerializer"/>, backed by <see cref="System.Text.Json"/> and the shared options.</summary>
public sealed class JsonSerializer : IJsonSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>Initializes a new instance of the <see cref="JsonSerializer"/> class using the shared default options.</summary>
    public JsonSerializer()
        : this(JsonOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="JsonSerializer"/> class with explicit options.</summary>
    /// <param name="options">The serializer options to use.</param>
    public JsonSerializer(JsonSerializerOptions options)
    {
        _options = Guard.AgainstNull(options);
    }

    /// <inheritdoc />
    public string Serialize<T>(T value) =>
        System.Text.Json.JsonSerializer.Serialize(value, _options);

    /// <inheritdoc />
    public T? Deserialize<T>(string json)
    {
        Guard.AgainstNull(json);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, _options);
    }

    /// <inheritdoc />
    public byte[] SerializeToUtf8Bytes<T>(T value) =>
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value, _options);

    /// <inheritdoc />
    public T? Deserialize<T>(ReadOnlySpan<byte> utf8Json) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(utf8Json, _options);
}
