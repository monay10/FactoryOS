namespace FactoryOS.Plugin.Configuration;

/// <summary>
/// Stable constants for the FactoryOS plugin framework: configuration section names and the default
/// health/heartbeat values shared by the options types, services and sample configuration.
/// </summary>
public static class PluginConstants
{
    /// <summary>The root configuration section the <see cref="PluginOptions"/> bind from.</summary>
    public const string ConfigurationSection = "Plugins";

    /// <summary>The configuration section the discovery options bind from.</summary>
    public const string DiscoverySection = "Plugins:Discovery";

    /// <summary>The configuration section the catalog options bind from.</summary>
    public const string CatalogSection = "Plugins:Catalog";

    /// <summary>The configuration section per-plugin configuration is read from (child sections keyed by plugin key).</summary>
    public const string PluginConfigurationSection = "Plugins:Configuration";

    /// <summary>The special per-plugin configuration key that toggles a plugin on or off.</summary>
    public const string EnabledKey = "Enabled";

    /// <summary>The default heartbeat interval, in seconds, a healthy plugin is expected to beat within.</summary>
    public const int DefaultHeartbeatIntervalSeconds = 30;

    /// <summary>The default number of missed heartbeats after which a plugin is considered unhealthy.</summary>
    public const int DefaultUnhealthyAfterMissedHeartbeats = 3;

    /// <summary>The default number of consecutive failures that latches a plugin into the failed state.</summary>
    public const int DefaultFailureThreshold = 3;
}

/// <summary>Plugin discovery options, bound from <see cref="PluginConstants.DiscoverySection"/>.</summary>
public sealed class PluginDiscoveryOptions
{
    /// <summary>Gets or sets a value indicating whether directory discovery runs.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the root directory whose immediate subfolders each hold a plugin manifest.</summary>
    public string RootPath { get; set; } = "plugins";
}

/// <summary>Plugin catalog options, bound from <see cref="PluginConstants.CatalogSection"/>.</summary>
public sealed class PluginCatalogOptions
{
    /// <summary>Gets or sets a value indicating whether the catalog is published.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets the additional manifest source directories the catalog includes beyond the discovery root.</summary>
    public IList<string> AdditionalSources { get; } = [];
}

/// <summary>Plugin health/heartbeat options.</summary>
public sealed class PluginHealthOptions
{
    /// <summary>Gets or sets the heartbeat interval, in seconds.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = PluginConstants.DefaultHeartbeatIntervalSeconds;

    /// <summary>Gets or sets the number of missed heartbeats after which a plugin reads as unhealthy.</summary>
    public int UnhealthyAfterMissedHeartbeats { get; set; } = PluginConstants.DefaultUnhealthyAfterMissedHeartbeats;

    /// <summary>Gets or sets the number of consecutive failures that latches a plugin as failed.</summary>
    public int FailureThreshold { get; set; } = PluginConstants.DefaultFailureThreshold;
}

/// <summary>
/// The plugin framework options, bound from <see cref="PluginConstants.ConfigurationSection"/>. The
/// <see cref="Discovery"/> and <see cref="Catalog"/> children map to their nested sections.
/// </summary>
public sealed class PluginOptions
{
    /// <summary>Gets or sets a value indicating whether plugins auto-start after their services are configured.</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>Gets or sets the discovery options.</summary>
    public PluginDiscoveryOptions Discovery { get; set; } = new();

    /// <summary>Gets or sets the catalog options.</summary>
    public PluginCatalogOptions Catalog { get; set; } = new();

    /// <summary>Gets or sets the health options.</summary>
    public PluginHealthOptions Health { get; set; } = new();
}
