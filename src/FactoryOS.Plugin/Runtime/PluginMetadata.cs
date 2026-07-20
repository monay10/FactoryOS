using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Plugin.Runtime;

/// <summary>
/// The read-model projection of a plugin manifest: the stable identity and capability surface the catalog
/// exposes, without the loading detail. Derived from a <see cref="PluginManifest"/>.
/// </summary>
/// <param name="Key">The plugin key.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="Version">The plugin version.</param>
/// <param name="Description">An optional description.</param>
/// <param name="Author">An optional author.</param>
/// <param name="Capabilities">The capability keys the plugin provides.</param>
/// <param name="Dependencies">The plugins this plugin depends upon.</param>
public sealed record PluginMetadata(
    string Key,
    string Name,
    PluginVersion Version,
    string? Description,
    string? Author,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<PluginDependency> Dependencies)
{
    /// <summary>Projects a manifest into its metadata.</summary>
    /// <param name="manifest">The manifest to project.</param>
    /// <returns>The metadata.</returns>
    public static PluginMetadata FromManifest(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new PluginMetadata(
            manifest.Key,
            manifest.Name,
            manifest.Version,
            manifest.Description,
            manifest.Author,
            manifest.Provides,
            manifest.Dependencies);
    }
}
