using FactoryOS.Connectors.Webhook;
using FactoryOS.Connectors.Webhook.Domain;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Tests.ConnectorWebhook;

public sealed class WebhookConnectorTests
{
    private sealed class RecordingSender : IWebhookSender
    {
        public List<(Uri Endpoint, OutboundMessage Message)> Sends { get; } = [];

        public Task<OutboundResult> SendAsync(Uri endpoint, OutboundMessage message, CancellationToken cancellationToken)
        {
            Sends.Add((endpoint, message));
            return Task.FromResult(OutboundResult.Ok("HTTP 200"));
        }
    }

    private static OutboundMessage Message(string channel = "ops") => new()
    {
        Tenant = "acme",
        Channel = channel,
        Priority = "Critical",
        Subject = "Safety stand-down",
        Action = "Notify",
        OccurredAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task Delivery_posts_to_the_resolved_endpoint()
    {
        var sender = new RecordingSender();
        var options = new WebhookConnectorOptions
        {
            ChannelUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ops"] = "https://hooks.example.com/ops" },
        };
        var connector = new WebhookConnector(sender, options);

        var result = await connector.DeliverAsync(Message(), CancellationToken.None);

        Assert.True(result.Delivered);
        var send = Assert.Single(sender.Sends);
        Assert.Equal(new Uri("https://hooks.example.com/ops"), send.Endpoint);
        Assert.Equal("webhook", connector.Transport);
    }

    [Fact]
    public async Task Delivery_fails_when_no_endpoint_is_configured()
    {
        var connector = new WebhookConnector(new RecordingSender(), new WebhookConnectorOptions());

        var result = await connector.DeliverAsync(Message(), CancellationToken.None);

        Assert.False(result.Delivered);
        Assert.Contains("no endpoint", result.Detail, StringComparison.Ordinal);
    }
}
