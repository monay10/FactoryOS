namespace FactoryOS.Connectors.Webhook.Domain;

/// <summary>
/// Resolves the endpoint URL a channel's notifications POST to, per the tenant's configured routing. Pure lookup
/// with a configured fallback; returns the parsed absolute <see cref="Uri"/>, or <see langword="null"/> when no
/// valid endpoint is configured. Routing is data, not code.
/// </summary>
public static class EndpointResolver
{
    /// <summary>Resolves the endpoint for a channel.</summary>
    /// <param name="channel">The logical channel.</param>
    /// <param name="options">The connector options carrying the URL table and default.</param>
    /// <returns>The absolute endpoint URI, or <see langword="null"/> if none is configured or it is not a valid absolute URL.</returns>
    public static Uri? Resolve(string channel, WebhookConnectorOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(options);

        var url = options.ChannelUrls.TryGetValue(channel, out var mapped) && !string.IsNullOrWhiteSpace(mapped)
            ? mapped
            : options.DefaultUrl;

        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
    }
}
