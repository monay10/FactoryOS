using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Webhook.Domain;

/// <summary>
/// The default <see cref="IWebhookSender"/>: POSTs the message as JSON to the endpoint over HTTP. A non-success
/// status or a transport error is a failed <see cref="OutboundResult"/>, never an unhandled exception — so the
/// connector always reports an outcome the audit trail can record.
/// </summary>
public sealed class HttpWebhookSender : IWebhookSender
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="HttpWebhookSender"/> class.</summary>
    /// <param name="httpClient">The HTTP client to send with.</param>
    public HttpWebhookSender(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<OutboundResult> SendAsync(Uri endpoint, OutboundMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(message);

        var json = JsonSerializer.Serialize(message, SerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? OutboundResult.Ok(string.Format(CultureInfo.InvariantCulture, "HTTP {0}", status))
                : OutboundResult.Failed(string.Format(CultureInfo.InvariantCulture, "HTTP {0}", status));
        }
        catch (HttpRequestException ex)
        {
            return OutboundResult.Failed($"transport error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OutboundResult.Failed("timeout");
        }
    }
}
