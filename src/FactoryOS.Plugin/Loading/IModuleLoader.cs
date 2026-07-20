using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;
using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Plugin.Loading;

/// <summary>
/// Turns a discovered <see cref="PluginDescriptor"/> into a live <see cref="IPlugin"/> instance by
/// loading its entry assembly in isolation and activating its entry type. This is the "module loader"
/// seam of the modular monolith: the core loads a plugin from its manifest alone, never by name.
/// </summary>
public interface IModuleLoader
{
    /// <summary>Loads and activates the plugin described by <paramref name="descriptor"/>.</summary>
    /// <param name="descriptor">The descriptor whose manifest and location identify the plugin.</param>
    /// <returns>
    /// A successful result carrying the activated <see cref="IPlugin"/>, or a failure describing why
    /// the plugin could not be loaded.
    /// </returns>
    Result<IPlugin> Load(PluginDescriptor descriptor);
}
