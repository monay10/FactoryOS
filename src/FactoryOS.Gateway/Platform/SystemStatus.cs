namespace FactoryOS.Gateway.Platform;

/// <summary>
/// The operating system presenting itself: one call that answers "what is this factory running, and is it
/// healthy?" — the platform identity and version, how many plugins are installed and active, how many need
/// attention (an unmet dependency), the union of capabilities the active plugins provide, and how many distinct
/// event types flow across them. A capstone rollup of the gateway's discovery surface, built purely from
/// manifests: the same core answers differently for every factory solely by which plugins are active.
/// </summary>
/// <param name="Product">The product name (for example <c>FactoryOS Enterprise</c>).</param>
/// <param name="Version">The platform version.</param>
/// <param name="ModulesInstalled">How many plugins are installed on the host, in any state.</param>
/// <param name="ModulesActive">How many of those are active (neither disabled nor failed).</param>
/// <param name="PluginsNeedingAttention">How many installed plugins have at least one unmet dependency.</param>
/// <param name="Capabilities">The sorted union of capability keys the active plugins provide.</param>
/// <param name="EventTypes">How many distinct event types the active plugins consume or emit.</param>
public sealed record SystemStatus(
    string Product,
    string Version,
    int ModulesInstalled,
    int ModulesActive,
    int PluginsNeedingAttention,
    IReadOnlyList<string> Capabilities,
    int EventTypes);
