namespace FactoryOS.Plugins.Notification.Domain;

/// <summary>
/// Resolves a logical channel to the transport that delivers it, per the tenant's configured routing. Pure
/// lookup with a configured fallback — no I/O, no branching on customer. Mapping is data, not code.
/// </summary>
public static class TransportResolver
{
    /// <summary>Resolves the transport for a channel, falling back to the default when unmapped.</summary>
    /// <param name="channel">The logical channel the action targeted.</param>
    /// <param name="options">The module options carrying the routing table and default.</param>
    /// <returns>The transport name to dispatch on.</returns>
    public static string Resolve(string channel, NotificationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(options);

        return options.ChannelTransports.TryGetValue(channel, out var transport) && !string.IsNullOrWhiteSpace(transport)
            ? transport
            : options.DefaultTransport;
    }
}
