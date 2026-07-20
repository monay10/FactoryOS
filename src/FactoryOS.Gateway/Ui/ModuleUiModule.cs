using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Gateway.Ui;

/// <summary>The UI contribution of a single active module within the <see cref="ModuleUiCatalog"/>.</summary>
/// <param name="Key">The module's manifest key.</param>
/// <param name="Name">The module's human-readable name.</param>
/// <param name="Version">The module's version, as a display string.</param>
/// <param name="Screens">The screens the module contributes, ordered for navigation.</param>
public sealed record ModuleUiModule(
    string Key,
    string Name,
    string Version,
    IReadOnlyList<PluginUiScreen> Screens);
