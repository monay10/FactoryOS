using FactoryOS.Plugin.Health;
using FactoryOS.Plugin.Registry;
using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Plugin.Catalog;

/// <summary>
/// A read model over the registered plugins: their metadata, capability index and health. The catalog is
/// the query surface a management UI or the Store (Phase 5) reads; it never mutates plugin state.
/// </summary>
public interface IPluginCatalog
{
    /// <summary>Lists the metadata of every registered plugin.</summary>
    /// <returns>The plugin metadata.</returns>
    IReadOnlyCollection<PluginMetadata> List();

    /// <summary>Finds a plugin's metadata by key.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns>The metadata, or <see langword="null"/> when no plugin has that key.</returns>
    PluginMetadata? Find(string key);

    /// <summary>Lists the plugins that provide a given capability.</summary>
    /// <param name="capability">The capability key.</param>
    /// <returns>The matching plugins' metadata.</returns>
    IReadOnlyCollection<PluginMetadata> WithCapability(string capability);

    /// <summary>Gets the health snapshot of every tracked plugin.</summary>
    /// <returns>The health snapshots.</returns>
    IReadOnlyCollection<PluginHealth> Health();
}

/// <summary>Default <see cref="IPluginCatalog"/> projecting the registry and health service.</summary>
public sealed class PluginCatalog : IPluginCatalog
{
    private readonly IPluginRegistry _registry;
    private readonly IPluginHealthService _health;

    /// <summary>Initializes a new instance of the <see cref="PluginCatalog"/> class.</summary>
    /// <param name="registry">The plugin registry.</param>
    /// <param name="health">The plugin health service.</param>
    public PluginCatalog(IPluginRegistry registry, IPluginHealthService health)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(health);
        _registry = registry;
        _health = health;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PluginMetadata> List() =>
        _registry.All.Select(descriptor => PluginMetadata.FromManifest(descriptor.Manifest)).ToArray();

    /// <inheritdoc />
    public PluginMetadata? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var descriptor = _registry.Find(key);
        return descriptor is null ? null : PluginMetadata.FromManifest(descriptor.Manifest);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PluginMetadata> WithCapability(string capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        var wanted = PluginCapability.Parse(capability).Key;

        return List()
            .Where(metadata => metadata.Capabilities.Any(
                provided => string.Equals(PluginCapability.Parse(provided).Key, wanted, StringComparison.Ordinal)))
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PluginHealth> Health() => _health.All();
}
