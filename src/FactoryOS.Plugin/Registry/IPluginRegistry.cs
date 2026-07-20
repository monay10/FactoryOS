using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Plugin.Registry;

/// <summary>
/// The authoritative, thread-safe catalogue of known plugins. The registry tracks each plugin's
/// descriptor and mediates enable/disable requests.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>Gets a snapshot of all registered plugin descriptors.</summary>
    IReadOnlyCollection<PluginDescriptor> All { get; }

    /// <summary>Finds a descriptor by plugin key.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns>The descriptor, or <see langword="null"/> when no plugin with that key is registered.</returns>
    PluginDescriptor? Find(string key);

    /// <summary>Registers a descriptor, replacing any existing entry with the same key.</summary>
    /// <param name="descriptor">The descriptor to register.</param>
    void Register(PluginDescriptor descriptor);

    /// <summary>Enables a plugin so the host will load it.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns><see langword="true"/> when a plugin with that key exists; otherwise <see langword="false"/>.</returns>
    bool Enable(string key);

    /// <summary>Disables a plugin so the host will skip it.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns><see langword="true"/> when a plugin with that key exists; otherwise <see langword="false"/>.</returns>
    bool Disable(string key);
}
