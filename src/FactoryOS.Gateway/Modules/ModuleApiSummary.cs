using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Gateway.Modules;

/// <summary>A serializable view of one active module's declared HTTP read routes, for the gateway's API discovery
/// endpoint. Aggregated from manifests so a client can find a module's endpoints as data, never by name.</summary>
/// <param name="Key">The module's manifest key.</param>
/// <param name="Name">The module's human-readable name.</param>
/// <param name="Routes">The HTTP read routes the module declares.</param>
public sealed record ModuleApiSummary(string Key, string Name, IReadOnlyList<PluginApiRoute> Routes);
