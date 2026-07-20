namespace FactoryOS.Contracts.Plugins;

/// <summary>The lifecycle state of a plugin as tracked by the framework.</summary>
public enum PluginState
{
    /// <summary>The plugin's manifest has been discovered but the plugin is not yet loaded.</summary>
    Discovered = 0,

    /// <summary>The plugin is intentionally switched off and will be skipped by the host.</summary>
    Disabled = 1,

    /// <summary>The plugin instance has been created and its services have been configured.</summary>
    Loaded = 2,

    /// <summary>The plugin has completed its start-up and is running.</summary>
    Started = 3,

    /// <summary>The plugin failed to load or start; see the recorded failure reason.</summary>
    Failed = 4,
}
