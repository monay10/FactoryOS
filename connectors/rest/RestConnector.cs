using System.Runtime.CompilerServices;
using System.Text.Json;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Rest;

/// <summary>
/// A generic connector that GETs a JSON resource and yields one <see cref="SourceRecord"/> per object in
/// a configured array. Scalar JSON values become field values; interpretation is left to the normalizer.
/// </summary>
public sealed class RestConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "rest";

    private readonly HttpClient _httpClient;
    private readonly RestConnectorOptions _options;

    /// <summary>Initializes a new instance of the <see cref="RestConnector"/> class.</summary>
    /// <param name="httpClient">The HTTP client used to read the resource (its base address is honoured).</param>
    /// <param name="options">The REST source configuration.</param>
    public RestConnector(HttpClient httpClient, RestConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RequestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SourceEntity);
        _httpClient = httpClient;
        _options = options;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "REST";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceRecord> ReadAsync(
        ConnectorReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var response = await _httpClient
            .GetAsync(new Uri(_options.RequestPath, UriKind.RelativeOrAbsolute), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!TryResolveArray(document.RootElement, out var array))
        {
            yield break;
        }

        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                fields[property.Name] = ToClrValue(property.Value);
            }

            yield return new SourceRecord(_options.SourceEntity, fields);
        }
    }

    private bool TryResolveArray(JsonElement root, out JsonElement array)
    {
        var current = root;

        if (!string.IsNullOrWhiteSpace(_options.ArrayPath))
        {
            foreach (var segment in _options.ArrayPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    array = default;
                    return false;
                }
            }
        }

        if (current.ValueKind != JsonValueKind.Array)
        {
            array = default;
            return false;
        }

        array = current;
        return true;
    }

    private static object? ToClrValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : (object)element.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText(),
    };
}
