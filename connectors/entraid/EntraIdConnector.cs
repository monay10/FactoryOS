using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.EntraId;

/// <summary>
/// A connector for Microsoft Entra ID (Azure AD) via the Microsoft Graph REST API. It GETs the users and
/// groups collections and yields one raw <see cref="SourceRecord"/> per object in each <c>value</c> array,
/// tagged <c>USERS</c> or <c>GROUPS</c>. Graph's dialect (<c>userPrincipalName</c>, <c>accountEnabled</c>, …)
/// is normalized to the Standard Model by the mapping — the connector never speaks Standard Model.
/// </summary>
public sealed class EntraIdConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "entraid";

    /// <summary>The source-entity tag for user records.</summary>
    public const string UsersEntity = "USERS";

    /// <summary>The source-entity tag for group records.</summary>
    public const string GroupsEntity = "GROUPS";

    private readonly HttpClient _httpClient;
    private readonly EntraIdConnectorOptions _options;

    /// <summary>Initializes a new instance of the <see cref="EntraIdConnector"/> class.</summary>
    /// <param name="httpClient">The HTTP client; its base address points at the Graph endpoint.</param>
    /// <param name="options">The Graph paths and access token.</param>
    public EntraIdConnector(HttpClient httpClient, EntraIdConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "EntraID";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceRecord> ReadAsync(
        ConnectorReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var record in await ReadCollectionAsync(_options.UsersPath, UsersEntity, cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }

        foreach (var record in await ReadCollectionAsync(_options.GroupsPath, GroupsEntity, cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }

    private async Task<IReadOnlyList<SourceRecord>> ReadCollectionAsync(
        string path,
        string entity,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.RelativeOrAbsolute));
        if (!string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var records = new List<SourceRecord>();
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return records;
        }

        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                fields[property.Name] = ReadScalar(property.Value);
            }

            records.Add(new SourceRecord(entity, fields));
        }

        return records;
    }

    private static object? ReadScalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => value.TryGetInt64(out var l) ? l : value.GetDouble(),
        _ => null,
    };
}
