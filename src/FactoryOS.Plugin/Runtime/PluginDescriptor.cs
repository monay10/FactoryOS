using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Plugin.Runtime;

/// <summary>
/// The framework's runtime view of a plugin: its manifest plus mutable lifecycle state, the resolved
/// instance and, on failure, the reason. Descriptors are the registry's unit of record.
/// </summary>
public sealed class PluginDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="PluginDescriptor"/> class.</summary>
    /// <param name="manifest">The plugin manifest.</param>
    /// <param name="location">The directory the plugin was discovered in, if any.</param>
    public PluginDescriptor(PluginManifest manifest, string? location = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        Manifest = manifest;
        Location = location;
        State = PluginState.Discovered;
    }

    /// <summary>Gets the plugin manifest.</summary>
    public PluginManifest Manifest { get; }

    /// <summary>Gets the plugin key (a shortcut for <c>Manifest.Key</c>).</summary>
    public string Key => Manifest.Key;

    /// <summary>Gets the directory the plugin was discovered in, if any.</summary>
    public string? Location { get; }

    /// <summary>Gets the current lifecycle state.</summary>
    public PluginState State { get; private set; }

    /// <summary>Gets the resolved plugin instance, once loaded.</summary>
    public IPlugin? Instance { get; private set; }

    /// <summary>Gets the failure reason when <see cref="State"/> is <see cref="PluginState.Failed"/>.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Attaches the loaded plugin instance and moves the descriptor to <see cref="PluginState.Loaded"/>.</summary>
    /// <param name="instance">The plugin instance.</param>
    public void AttachInstance(IPlugin instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        Instance = instance;
        FailureReason = null;
        State = PluginState.Loaded;
    }

    /// <summary>Marks the plugin as started.</summary>
    public void MarkStarted() => State = PluginState.Started;

    /// <summary>Returns a started plugin to the loaded state after it stops (its instance stays attached).</summary>
    public void MarkStopped() => State = PluginState.Loaded;

    /// <summary>Marks the plugin as disabled, so the host skips it.</summary>
    public void MarkDisabled() => State = PluginState.Disabled;

    /// <summary>Re-enables a disabled plugin, returning it to <see cref="PluginState.Discovered"/>.</summary>
    public void MarkDiscovered() => State = PluginState.Discovered;

    /// <summary>Marks the plugin as failed and records why.</summary>
    /// <param name="reason">A human-readable failure reason.</param>
    public void MarkFailed(string reason)
    {
        FailureReason = reason;
        State = PluginState.Failed;
    }
}
