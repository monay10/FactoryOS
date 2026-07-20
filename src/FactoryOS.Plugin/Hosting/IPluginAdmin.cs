namespace FactoryOS.Plugin.Hosting;

/// <summary>The outcome of a plugin enable/disable request.</summary>
public enum PluginAdminOutcome
{
    /// <summary>The plugin's state was changed.</summary>
    Changed,

    /// <summary>The request was a no-op — the plugin was already in the requested state.</summary>
    Unchanged,

    /// <summary>No installed plugin has the requested key.</summary>
    NotFound,

    /// <summary>The plugin is in a failed state and cannot be toggled until it is fixed and reloaded.</summary>
    Failed,
}

/// <summary>The result of a plugin admin operation.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Key">The plugin key the request targeted.</param>
/// <param name="State">The plugin's state after the operation, or <see langword="null"/> when not found.</param>
public sealed record PluginAdminResult(PluginAdminOutcome Outcome, string Key, string? State);

/// <summary>
/// The administrative control surface for installed plugins — the write side of the Store (Phase 5). It toggles a
/// plugin's activation without any customer-specific branching: the same operation works for every tenant, and
/// which plugins exist is still discovered from manifests. Disabling hides a plugin from the discovery surface and
/// marks it switched off; enabling returns it to service.
/// </summary>
public interface IPluginAdmin
{
    /// <summary>Enables or disables an installed plugin by key.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="enabled"><see langword="true"/> to enable the plugin; <see langword="false"/> to disable it.</param>
    /// <returns>The outcome and the plugin's resulting state.</returns>
    PluginAdminResult SetEnabled(string key, bool enabled);
}
