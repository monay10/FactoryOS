namespace FactoryOS.Gateway.Store;

/// <summary>
/// The marketplace view of everything installed on this host: every plugin the host knows — active,
/// disabled or failed — with the package metadata a Store lists and, for each declared dependency, whether
/// a satisfying plugin is currently active. It is the read foundation of the marketplace (Phase 5); like all
/// gateway discovery, it is built purely from manifests, so it reflects exactly what is installed with no
/// core-side branching.
/// </summary>
/// <param name="Plugins">The installed plugins, ordered by key.</param>
public sealed record StoreCatalog(IReadOnlyList<StorePlugin> Plugins);

/// <summary>One plugin's marketplace entry.</summary>
/// <param name="Key">The plugin's manifest key.</param>
/// <param name="Name">The plugin's human-readable name.</param>
/// <param name="Version">The installed version, as a display string.</param>
/// <param name="Description">The plugin's description, if any.</param>
/// <param name="Author">The plugin's author, if any.</param>
/// <param name="State">The plugin's lifecycle state (for example <c>Started</c> or <c>Disabled</c>).</param>
/// <param name="Provides">The capability keys the plugin advertises.</param>
/// <param name="Dependencies">The plugin's declared dependencies, each with its satisfaction status.</param>
public sealed record StorePlugin(
    string Key,
    string Name,
    string Version,
    string? Description,
    string? Author,
    string State,
    IReadOnlyList<string> Provides,
    IReadOnlyList<StoreDependency> Dependencies);

/// <summary>A plugin's declared dependency, resolved against the currently active plugins.</summary>
/// <param name="PluginKey">The key of the plugin that must be present.</param>
/// <param name="MinimumVersion">The lowest version that satisfies the dependency.</param>
/// <param name="Satisfied">Whether an active plugin with this key is installed at a satisfying version.</param>
public sealed record StoreDependency(string PluginKey, string MinimumVersion, bool Satisfied);

/// <summary>
/// A marketplace health headline for an at-a-glance strip: how many plugins are installed, a per-state tally,
/// and how many have at least one unmet dependency (a Store's "needs attention" count).
/// </summary>
/// <param name="Total">How many plugins are installed on the host.</param>
/// <param name="ByState">A per-state tally, ordered by count descending (ties broken by state name).</param>
/// <param name="WithUnmetDependencies">How many plugins have at least one unsatisfied dependency.</param>
public sealed record StoreSummary(
    int Total,
    IReadOnlyList<StoreStateTally> ByState,
    int WithUnmetDependencies);

/// <summary>One row of the Store summary's per-state breakdown.</summary>
/// <param name="State">The lifecycle state (for example <c>Started</c> or <c>Disabled</c>).</param>
/// <param name="Count">How many plugins are in this state.</param>
public sealed record StoreStateTally(string State, int Count);
