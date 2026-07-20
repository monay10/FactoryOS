using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Gateway.Ui;

/// <summary>
/// Default <see cref="IModuleUiCatalogProvider"/>. Aggregates the UI screens declared in every active
/// module's manifest into a single, deterministically ordered catalog. Disabled and failed modules
/// contribute nothing, so behaviour varies purely by which plugins are active.
/// </summary>
public sealed class ModuleUiCatalogProvider : IModuleUiCatalogProvider
{
    private readonly IPluginHost _host;

    /// <summary>Initializes a new instance of the <see cref="ModuleUiCatalogProvider"/> class.</summary>
    /// <param name="host">The plugin host whose active modules are surfaced.</param>
    public ModuleUiCatalogProvider(IPluginHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <inheritdoc />
    public ModuleUiCatalog GetCatalog()
    {
        var modules = _host.Plugins
            .Where(IsActive)
            .Where(descriptor => descriptor.Manifest.Ui.Count > 0)
            .OrderBy(descriptor => descriptor.Key, StringComparer.Ordinal)
            .Select(ToModule)
            .ToArray();

        return new ModuleUiCatalog(modules);
    }

    /// <inheritdoc />
    public NavCatalog GetNavigation()
    {
        var sections = _host.Plugins
            .Where(IsActive)
            .Where(descriptor => descriptor.Manifest.Ui.Count > 0)
            .SelectMany(descriptor => descriptor.Manifest.Ui.Select(screen => (descriptor.Manifest.Key, screen)))
            .GroupBy(pair => pair.screen.NavSection ?? string.Empty, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new NavSection(
                group.Key,
                group
                    .OrderBy(pair => pair.screen.Order)
                    .ThenBy(pair => pair.screen.Title, StringComparer.Ordinal)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new NavItem(
                        pair.Key,
                        pair.screen.Id,
                        pair.screen.Title,
                        pair.screen.Route,
                        pair.screen.Component,
                        pair.screen.Icon,
                        pair.screen.RequiredPermission,
                        pair.screen.Order))
                    .ToArray()))
            .ToArray();

        return new NavCatalog(sections);
    }

    private static bool IsActive(PluginDescriptor descriptor) =>
        descriptor.State is not (PluginState.Disabled or PluginState.Failed);

    private static ModuleUiModule ToModule(PluginDescriptor descriptor)
    {
        var screens = descriptor.Manifest.Ui
            .OrderBy(screen => screen.NavSection ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(screen => screen.Order)
            .ThenBy(screen => screen.Title, StringComparer.Ordinal)
            .ToArray();

        return new ModuleUiModule(
            descriptor.Manifest.Key,
            descriptor.Manifest.Name,
            descriptor.Manifest.Version.ToString(),
            screens);
    }
}
