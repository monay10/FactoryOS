namespace FactoryOS.Contracts.Plugins;

/// <summary>
/// The declarative description of a plugin (its <c>module.json</c>). Manifests are <b>data, not
/// code</b>: the core discovers and wires plugins through their manifests and never references a
/// plugin by name.
/// </summary>
public sealed record PluginManifest
{
    /// <summary>Gets the stable, unique key that identifies the plugin.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the human-readable plugin name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the plugin version.</summary>
    public required PluginVersion Version { get; init; }

    /// <summary>Gets an optional human-readable description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the optional plugin author.</summary>
    public string? Author { get; init; }

    /// <summary>Gets the file name of the entry assembly used when the plugin is loaded in isolation.</summary>
    public string? Assembly { get; init; }

    /// <summary>Gets the full name of the type implementing <see cref="IPlugin"/>.</summary>
    public string? EntryType { get; init; }

    /// <summary>Gets the plugins this plugin depends upon.</summary>
    public IReadOnlyList<PluginDependency> Dependencies { get; init; } = [];

    /// <summary>Gets the capability keys this plugin provides.</summary>
    public IReadOnlyList<string> Provides { get; init; } = [];

    /// <summary>Gets the event types this plugin consumes.</summary>
    public IReadOnlyList<string> Consumes { get; init; } = [];

    /// <summary>Gets the event types this plugin emits.</summary>
    public IReadOnlyList<string> Emits { get; init; } = [];

    /// <summary>Gets the UI screens this plugin contributes to the shell's lazy-load registry.</summary>
    public IReadOnlyList<PluginUiScreen> Ui { get; init; } = [];

    /// <summary>Gets the HTTP read routes this plugin contributes through the gateway.</summary>
    public IReadOnlyList<PluginApiRoute> Api { get; init; } = [];
}
