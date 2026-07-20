namespace FactoryOS.Configuration.Model;

/// <summary>
/// The strongly-typed representation of a tenant's <c>tenant.json</c>: the single artifact that
/// onboards a factory. A new customer is one <see cref="TenantConfiguration"/> — modules, plugins,
/// branding, localization and environment — and never a core code change.
/// </summary>
public sealed record TenantConfiguration
{
    /// <summary>Gets the stable, unique tenant identifier (e.g. <c>tenant_001</c>).</summary>
    public required string TenantId { get; init; }

    /// <summary>Gets the tenant's human-readable name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the deployment environment the tenant runs in.</summary>
    public DeploymentEnvironment Environment { get; init; } = DeploymentEnvironment.Production;

    /// <summary>Gets the tenant branding.</summary>
    public TenantBranding? Branding { get; init; }

    /// <summary>Gets the tenant localization.</summary>
    public TenantLocalization? Localization { get; init; }

    /// <summary>Gets the configured business modules.</summary>
    public IReadOnlyList<ModuleConfiguration> Modules { get; init; } = [];

    /// <summary>Gets the configured plugins (connectors, agents, dashboards).</summary>
    public IReadOnlyList<PluginConfiguration> Plugins { get; init; } = [];

    /// <summary>Finds a module configuration by key.</summary>
    /// <param name="key">The module key.</param>
    /// <returns>The module configuration, or <see langword="null"/> when not present.</returns>
    public ModuleConfiguration? GetModule(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Modules.FirstOrDefault(module => string.Equals(module.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Determines whether a module is present and enabled.</summary>
    /// <param name="key">The module key.</param>
    /// <returns><see langword="true"/> when the module is configured and enabled.</returns>
    public bool IsModuleEnabled(string key) => GetModule(key) is { Enabled: true };

    /// <summary>Finds a plugin configuration by key.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns>The plugin configuration, or <see langword="null"/> when not present.</returns>
    public PluginConfiguration? GetPlugin(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Plugins.FirstOrDefault(plugin => string.Equals(plugin.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Determines whether a plugin is present and enabled.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns><see langword="true"/> when the plugin is configured and enabled.</returns>
    public bool IsPluginEnabled(string key) => GetPlugin(key) is { Enabled: true };
}
