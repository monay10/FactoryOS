using FactoryOS.Connectors.Webhook.Domain;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Webhook;

/// <summary>
/// An outbound connector that delivers notifications by POSTing them to a per-channel webhook endpoint. The
/// second outbound transport after the log connector — proof the outbound contract generalizes to a real
/// network door — and the reference for HTTP-based transports (Slack, Teams, custom receivers).
/// </summary>
public sealed class WebhookConnector : IOutboundConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "webhook";

    private readonly IWebhookSender _sender;
    private readonly WebhookConnectorOptions _options;

    /// <summary>Initializes a new instance of the <see cref="WebhookConnector"/> class.</summary>
    /// <param name="sender">The webhook sender.</param>
    /// <param name="options">The connector options carrying the transport name and endpoint routing.</param>
    public WebhookConnector(IWebhookSender sender, WebhookConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(options);
        _sender = sender;
        _options = options;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string Transport => _options.Transport;

    /// <inheritdoc />
    public Task<OutboundResult> DeliverAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var endpoint = EndpointResolver.Resolve(message.Channel, _options);
        if (endpoint is null)
        {
            return Task.FromResult(OutboundResult.Failed($"no endpoint configured for channel '{message.Channel}'"));
        }

        return _sender.SendAsync(endpoint, message, cancellationToken);
    }
}
