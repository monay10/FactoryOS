namespace FactoryOS.Contracts.Plugins;

/// <summary>
/// The declarative description of a single HTTP read route a plugin contributes through the gateway. Routes are
/// <b>data, not code</b>: the core aggregates them into a discovery catalog so a client can find a module's
/// endpoints from the manifest, never by referencing the plugin by name. The route is actually served by the
/// plugin's <c>IModuleApi</c> implementation; this manifest entry is its published contract.
/// </summary>
public sealed record PluginApiRoute
{
    /// <summary>Gets the HTTP method (for example <c>GET</c>).</summary>
    public required string Method { get; init; }

    /// <summary>Gets the gateway-absolute path the endpoint is mounted at (for example <c>/m/activity/feed</c>).</summary>
    public required string Path { get; init; }

    /// <summary>Gets the query-string parameters the endpoint accepts.</summary>
    public IReadOnlyList<string> Query { get; init; } = [];

    /// <summary>Gets an optional human-readable description of what the endpoint returns.</summary>
    public string? Description { get; init; }
}
