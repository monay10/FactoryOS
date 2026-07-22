using System.Collections.Concurrent;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Persistence;

namespace FactoryOS.Plugins.Runtime.Execution;

/// <summary>
/// The runtime's record of what is installed and, of that, what is currently a live object in memory.
/// <para>
/// The name is deliberately not <c>PluginRegistry</c>: the framework already has one, and it registers
/// <b>plugins</b> process-wide. This one registers <b>instances</b> — one tenant's installation of a plugin —
/// and every lookup takes the tenant, so the two can sit side by side without either shadowing the other.
/// </para>
/// </summary>
public sealed class PluginInstanceRegistry
{
    private readonly ConcurrentDictionary<string, IPlugin> _live = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPluginStore _store;
    private readonly IPluginRepository _repository;

    /// <summary>Initializes a new instance of the <see cref="PluginInstanceRegistry"/> class.</summary>
    /// <param name="store">The tenant installations.</param>
    /// <param name="repository">The definition catalogue.</param>
    public PluginInstanceRegistry(IPluginStore store, IPluginRepository repository)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);

        _store = store;
        _repository = repository;
    }

    /// <summary>Gets the number of plugin objects currently held in memory.</summary>
    public int LiveCount => _live.Count;

    /// <summary>Records an installation.</summary>
    /// <param name="instance">The installation.</param>
    public void Register(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _store.Save(instance);
    }

    /// <summary>Finds one tenant's installation of a plugin.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <returns>The installation, or <see langword="null"/> when the tenant has not installed it.</returns>
    public PluginInstance? Find(string tenant, string pluginKey) => _store.Find(tenant, pluginKey);

    /// <summary>Lists everything one tenant has installed.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The installations.</returns>
    public IReadOnlyList<PluginInstance> ForTenant(string tenant) => _store.ForTenant(tenant);

    /// <summary>Lists every installation across every tenant.</summary>
    /// <returns>The installations.</returns>
    public IReadOnlyList<PluginInstance> All() => _store.All();

    /// <summary>Finds the definition an installation runs.</summary>
    /// <param name="instance">The installation.</param>
    /// <returns>The definition, or <see langword="null"/> when the catalogue does not hold that version.</returns>
    public PluginDefinition? DefinitionFor(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return _repository.Find(instance.PluginKey, instance.Version);
    }

    /// <summary>Keeps the live plugin object an installation loaded.</summary>
    /// <param name="instance">The installation.</param>
    /// <param name="plugin">The plugin object.</param>
    public void Attach(PluginInstance instance, IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(plugin);
        _live[instance.Identity] = plugin;
    }

    /// <summary>Gets the live plugin object an installation loaded.</summary>
    /// <param name="instance">The installation.</param>
    /// <returns>The plugin object, or <see langword="null"/> when nothing is loaded.</returns>
    public IPlugin? Attached(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return _live.TryGetValue(instance.Identity, out var plugin) ? plugin : null;
    }

    /// <summary>Forgets the live plugin object an installation held.</summary>
    /// <param name="instance">The installation.</param>
    /// <returns><see langword="true"/> when an object was held.</returns>
    public bool Detach(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return _live.TryRemove(instance.Identity, out _);
    }

    /// <summary>Forgets an installation entirely.</summary>
    /// <param name="instance">The installation.</param>
    /// <returns><see langword="true"/> when it was recorded.</returns>
    public bool Remove(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Detach(instance);
        return _store.Remove(instance.Tenant, instance.PluginKey);
    }
}
