using System.Collections.Concurrent;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Persistence;

/// <summary>
/// The catalogue of plugin <b>definitions</b> the platform knows about, keyed by plugin and version.
/// <para>
/// Definitions are versioned rather than replaced, because a tenant that has not updated is still running an
/// older one and the runtime must be able to describe what that tenant is running.
/// </para>
/// </summary>
public interface IPluginRepository
{
    /// <summary>Records a definition, replacing any earlier record of the same plugin at the same version.</summary>
    /// <param name="definition">The definition.</param>
    void Save(PluginDefinition definition);

    /// <summary>Finds one version of a plugin.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="version">The version.</param>
    /// <returns>The definition, or <see langword="null"/> when that version is unknown.</returns>
    PluginDefinition? Find(string key, PluginVersion version);

    /// <summary>Finds the highest known version of a plugin.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns>The definition, or <see langword="null"/> when the plugin is unknown.</returns>
    PluginDefinition? Latest(string key);

    /// <summary>Lists every known version of a plugin, lowest first.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns>The definitions.</returns>
    IReadOnlyList<PluginDefinition> Versions(string key);

    /// <summary>Lists every definition the catalogue holds.</summary>
    /// <returns>The definitions.</returns>
    IReadOnlyList<PluginDefinition> All();

    /// <summary>Forgets one version of a plugin.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> when a definition was removed.</returns>
    bool Remove(string key, PluginVersion version);
}

/// <summary>
/// Where a tenant's plugin installations live. Every method takes the tenant, and the tenant is part of the
/// key the instance is filed under, so there is no call shape that could return another factory's plugin.
/// </summary>
public interface IPluginStore
{
    /// <summary>Records an installation, replacing any earlier one for the same tenant and plugin.</summary>
    /// <param name="instance">The installation.</param>
    void Save(PluginInstance instance);

    /// <summary>Finds one tenant's installation of a plugin.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <returns>The installation, or <see langword="null"/> when the tenant has not installed it.</returns>
    PluginInstance? Find(string tenant, string pluginKey);

    /// <summary>Lists everything one tenant has installed.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The installations.</returns>
    IReadOnlyList<PluginInstance> ForTenant(string tenant);

    /// <summary>Lists every installation across every tenant, for the host's own start-up sweep.</summary>
    /// <returns>The installations.</returns>
    IReadOnlyList<PluginInstance> All();

    /// <summary>Removes one tenant's installation of a plugin.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <returns><see langword="true"/> when an installation was removed.</returns>
    bool Remove(string tenant, string pluginKey);
}

/// <summary>The manifests exactly as they were read, so a definition can be re-projected without re-reading disk.</summary>
public interface IPluginManifestRepository
{
    /// <summary>Records a manifest.</summary>
    /// <param name="manifest">The manifest.</param>
    void Save(PluginManifest manifest);

    /// <summary>Finds one version of a manifest.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="version">The version.</param>
    /// <returns>The manifest, or <see langword="null"/> when that version is unknown.</returns>
    PluginManifest? Find(string key, PluginVersion version);

    /// <summary>Lists every manifest held.</summary>
    /// <returns>The manifests.</returns>
    IReadOnlyList<PluginManifest> All();
}

/// <summary>
/// Where package content and its signature live, including the versions an update superseded.
/// <para>
/// Retaining superseded packages is what makes <see cref="Domain.PluginLifecyclePhase.Rollback"/> real. An
/// update that deletes what it replaced has not made the system upgradable; it has made it one-way.
/// </para>
/// </summary>
public interface IPluginPackageStore
{
    /// <summary>Stores a package.</summary>
    /// <param name="package">The package.</param>
    void Save(PluginPackage package);

    /// <summary>Finds one version of a package.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="version">The version.</param>
    /// <returns>The package, or <see langword="null"/> when that version is not stored.</returns>
    PluginPackage? Find(string key, PluginVersion version);

    /// <summary>Lists every stored version of a plugin, lowest first.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns>The packages.</returns>
    IReadOnlyList<PluginPackage> Versions(string key);

    /// <summary>Lists every package stored.</summary>
    /// <returns>The packages.</returns>
    IReadOnlyList<PluginPackage> All();

    /// <summary>
    /// Drops the oldest stored versions of a plugin, keeping the newest <paramref name="retain"/> of them.
    /// </summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="retain">How many versions to keep; at least one is always kept.</param>
    /// <returns>The versions dropped.</returns>
    IReadOnlyList<PluginVersion> Prune(string key, int retain);
}

/// <summary>Default in-memory <see cref="IPluginRepository"/>.</summary>
public sealed class InMemoryPluginRepository : IPluginRepository
{
    private readonly ConcurrentDictionary<string, PluginDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Save(PluginDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Identity] = definition;
    }

    /// <inheritdoc />
    public PluginDefinition? Find(string key, PluginVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryGetValue(PluginDefinition.Identify(key, version), out var definition)
            ? definition
            : null;
    }

    /// <inheritdoc />
    public PluginDefinition? Latest(string key)
    {
        var versions = Versions(key);
        return versions.Count == 0 ? null : versions[^1];
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginDefinition> Versions(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return [.. _definitions.Values
            .Where(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(definition => definition.Version)];
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginDefinition> All() =>
        [.. _definitions.Values.OrderBy(definition => definition.Identity, StringComparer.Ordinal)];

    /// <inheritdoc />
    public bool Remove(string key, PluginVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryRemove(PluginDefinition.Identify(key, version), out _);
    }
}

/// <summary>Default in-memory <see cref="IPluginStore"/>, keyed by <c>tenant|plugin</c>.</summary>
public sealed class InMemoryPluginStore : IPluginStore
{
    private readonly ConcurrentDictionary<string, PluginInstance> _instances =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Save(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[instance.Identity] = instance;
    }

    /// <inheritdoc />
    public PluginInstance? Find(string tenant, string pluginKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);
        return _instances.TryGetValue(PluginInstance.Identify(tenant, pluginKey), out var instance)
            ? instance
            : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginInstance> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return [.. _instances.Values
            .Where(instance => string.Equals(instance.Tenant, tenant, StringComparison.OrdinalIgnoreCase))
            .OrderBy(instance => instance.PluginKey, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginInstance> All() =>
        [.. _instances.Values.OrderBy(instance => instance.Identity, StringComparer.Ordinal)];

    /// <inheritdoc />
    public bool Remove(string tenant, string pluginKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);
        return _instances.TryRemove(PluginInstance.Identify(tenant, pluginKey), out _);
    }
}

/// <summary>Default in-memory <see cref="IPluginManifestRepository"/>.</summary>
public sealed class InMemoryPluginManifestRepository : IPluginManifestRepository
{
    private readonly ConcurrentDictionary<string, PluginManifest> _manifests =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Save(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _manifests[PluginDefinition.Identify(manifest.Key, manifest.Version)] = manifest;
    }

    /// <inheritdoc />
    public PluginManifest? Find(string key, PluginVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _manifests.TryGetValue(PluginDefinition.Identify(key, version), out var manifest) ? manifest : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginManifest> All() =>
        [.. _manifests.Values.OrderBy(manifest => manifest.Key, StringComparer.Ordinal)
            .ThenBy(manifest => manifest.Version)];
}

/// <summary>Default in-memory <see cref="IPluginPackageStore"/>.</summary>
public sealed class InMemoryPluginPackageStore : IPluginPackageStore
{
    private readonly ConcurrentDictionary<string, PluginPackage> _packages =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Save(PluginPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        _packages[package.Identity] = package;
    }

    /// <inheritdoc />
    public PluginPackage? Find(string key, PluginVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _packages.TryGetValue(PluginDefinition.Identify(key, version), out var package) ? package : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginPackage> Versions(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return [.. _packages.Values
            .Where(package => string.Equals(package.Key, key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(package => package.Version)];
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginPackage> All() =>
        [.. _packages.Values.OrderBy(package => package.Identity, StringComparer.Ordinal)];

    /// <inheritdoc />
    public IReadOnlyList<PluginVersion> Prune(string key, int retain)
    {
        var versions = Versions(key);
        var keep = Math.Max(1, retain);
        if (versions.Count <= keep)
        {
            return [];
        }

        var dropped = new List<PluginVersion>();
        for (var index = 0; index < versions.Count - keep; index++)
        {
            if (_packages.TryRemove(versions[index].Identity, out _))
            {
                dropped.Add(versions[index].Version);
            }
        }

        return dropped;
    }
}
