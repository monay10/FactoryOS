using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Webhook.Domain;

/// <summary>
/// The narrow port over which the webhook connector reaches the network — abstracted so the connector's routing
/// and idempotency logic can be tested without real HTTP, and so the transport (HTTP today) can change beneath it.
/// </summary>
public interface IWebhookSender
{
    /// <summary>POSTs a message to an endpoint and reports the outcome.</summary>
    /// <param name="endpoint">The absolute endpoint URL.</param>
    /// <param name="message">The message to deliver.</param>
    /// <param name="cancellationToken">A token to cancel the send.</param>
    /// <returns>The delivery outcome.</returns>
    Task<OutboundResult> SendAsync(Uri endpoint, OutboundMessage message, CancellationToken cancellationToken);
}
