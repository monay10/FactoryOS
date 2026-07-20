using FactoryOS.Connectors.Webhook;
using FactoryOS.Connectors.Webhook.Domain;

namespace FactoryOS.Tests.ConnectorWebhook;

public sealed class EndpointResolverTests
{
    private static WebhookConnectorOptions Options(string? defaultUrl = null, params (string Channel, string Url)[] urls)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (channel, url) in urls)
        {
            map[channel] = url;
        }

        return new WebhookConnectorOptions { ChannelUrls = map, DefaultUrl = defaultUrl };
    }

    [Fact]
    public void A_mapped_channel_resolves_to_its_endpoint()
    {
        var uri = EndpointResolver.Resolve("ops", Options(urls: ("ops", "https://hooks.example.com/ops")));

        Assert.Equal(new Uri("https://hooks.example.com/ops"), uri);
    }

    [Fact]
    public void An_unmapped_channel_falls_back_to_the_default()
    {
        var uri = EndpointResolver.Resolve("misc", Options(defaultUrl: "https://hooks.example.com/default"));

        Assert.Equal(new Uri("https://hooks.example.com/default"), uri);
    }

    [Fact]
    public void No_configured_endpoint_resolves_to_null()
    {
        Assert.Null(EndpointResolver.Resolve("ops", Options()));
    }

    [Fact]
    public void An_invalid_url_resolves_to_null()
    {
        Assert.Null(EndpointResolver.Resolve("ops", Options(urls: ("ops", "not-a-url"))));
    }
}
