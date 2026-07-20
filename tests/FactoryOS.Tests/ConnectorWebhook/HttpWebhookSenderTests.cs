using System.Net;
using FactoryOS.Connectors.Webhook.Domain;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Tests.ConnectorWebhook;

public sealed class HttpWebhookSenderTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly bool _throw;

        public StubHandler(HttpStatusCode status, bool @throw = false)
        {
            _status = status;
            _throw = @throw;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            if (_throw)
            {
                throw new HttpRequestException("connection refused");
            }

            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }

    private static OutboundMessage Message() => new()
    {
        Tenant = "acme",
        Channel = "ops",
        Priority = "Critical",
        Subject = "Safety stand-down",
        Action = "Notify",
        OccurredAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task A_success_status_is_a_delivered_result()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var sender = new HttpWebhookSender(client);

        var result = await sender.SendAsync(new Uri("https://hooks.example.com/ops"), Message(), CancellationToken.None);

        Assert.True(result.Delivered);
        Assert.Equal(new Uri("https://hooks.example.com/ops"), handler.LastRequestUri);
    }

    [Fact]
    public async Task A_non_success_status_is_a_failed_result()
    {
        using var client = new HttpClient(new StubHandler(HttpStatusCode.InternalServerError));
        var sender = new HttpWebhookSender(client);

        var result = await sender.SendAsync(new Uri("https://hooks.example.com/ops"), Message(), CancellationToken.None);

        Assert.False(result.Delivered);
        Assert.Contains("500", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_transport_error_is_a_failed_result()
    {
        using var client = new HttpClient(new StubHandler(HttpStatusCode.OK, @throw: true));
        var sender = new HttpWebhookSender(client);

        var result = await sender.SendAsync(new Uri("https://hooks.example.com/ops"), Message(), CancellationToken.None);

        Assert.False(result.Delivered);
        Assert.Contains("transport error", result.Detail, StringComparison.Ordinal);
    }
}
