using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Plugin.Hosting;

/// <summary>
/// Default <see cref="IPluginAdmin"/>: toggles a plugin descriptor's lifecycle state on the host registry.
/// A failed plugin cannot be toggled (it must be fixed and reloaded first); every other transition is
/// idempotent. In the first-party modular monolith a plugin's services stay registered while it is disabled,
/// so re-enabling returns it straight to <see cref="PluginState.Started"/>; the out-of-process Store runtime
/// (Phase 5) will instead load and start the assembly on enable.
/// </summary>
public sealed class PluginAdmin : IPluginAdmin
{
    private readonly IPluginHost _host;

    /// <summary>Initializes a new instance of the <see cref="PluginAdmin"/> class.</summary>
    /// <param name="host">The plugin host whose descriptors are toggled.</param>
    public PluginAdmin(IPluginHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <inheritdoc />
    public PluginAdminResult SetEnabled(string key, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var descriptor = _host.Plugins.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        if (descriptor is null)
        {
            return new PluginAdminResult(PluginAdminOutcome.NotFound, key, null);
        }

        if (descriptor.State is PluginState.Failed)
        {
            return new PluginAdminResult(PluginAdminOutcome.Failed, descriptor.Key, descriptor.State.ToString());
        }

        var currentlyEnabled = descriptor.State is not PluginState.Disabled;
        if (currentlyEnabled == enabled)
        {
            return new PluginAdminResult(PluginAdminOutcome.Unchanged, descriptor.Key, descriptor.State.ToString());
        }

        if (enabled)
        {
            descriptor.MarkStarted();
        }
        else
        {
            descriptor.MarkDisabled();
        }

        return new PluginAdminResult(PluginAdminOutcome.Changed, descriptor.Key, descriptor.State.ToString());
    }
}
