namespace FactoryOS.Gateway.Ui;

/// <summary>Builds the <see cref="ModuleUiCatalog"/> from the currently active plugins.</summary>
public interface IModuleUiCatalogProvider
{
    /// <summary>Builds a snapshot of the UI registry across all active modules.</summary>
    /// <returns>The aggregated UI catalog.</returns>
    ModuleUiCatalog GetCatalog();

    /// <summary>
    /// Builds the shell navigation across all active modules: every screen flattened and regrouped by
    /// navigation section, ready to render a sidebar.
    /// </summary>
    /// <returns>The aggregated navigation catalog.</returns>
    NavCatalog GetNavigation();
}
