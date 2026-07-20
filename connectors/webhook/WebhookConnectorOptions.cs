namespace FactoryOS.Connectors.Webhook;

/// <summary>
/// Configuration for the webhook transport connector. The transport name and the per-channel endpoint URLs are
/// data — a factory points each channel at its own webhook (Slack, Teams, a custom receiver) purely by config.
/// URLs that embed secrets use <c>${secret:...}</c> placeholders resolved by the host, never inline secrets.
/// </summary>
public sealed record WebhookConnectorOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Connectors:Webhook";

    /// <summary>The transport name this connector delivers for. Dispatches on other transports are ignored.</summary>
    public string Transport { get; init; } = "webhook";

    /// <summary>Maps a logical channel (for example <c>ops</c>) to the endpoint URL its notifications POST to.</summary>
    public IReadOnlyDictionary<string, string> ChannelUrls { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The endpoint used when a channel has no explicit URL; delivery fails if this too is unset.</summary>
    public string? DefaultUrl { get; init; }
}
