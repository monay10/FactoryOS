using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Plugin.Discovery;

/// <summary>Discovers plugins by scanning a directory tree for plugin manifests.</summary>
public interface IPluginDiscovery
{
    /// <summary>Scans the immediate subdirectories of a root for <c>module.json</c> manifests.</summary>
    /// <param name="rootDirectory">The directory whose child folders each hold one plugin.</param>
    /// <returns>
    /// One descriptor per discovered plugin. A plugin whose manifest is invalid is returned in the
    /// <see cref="FactoryOS.Contracts.Plugins.PluginState.Failed"/> state with the reason recorded.
    /// </returns>
    IReadOnlyList<PluginDescriptor> Discover(string rootDirectory);
}
