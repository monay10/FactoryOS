using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Plugin.Lifecycle;

/// <summary>
/// The optional extended lifecycle a plugin may implement in addition to <see cref="Contracts.Plugins.IPlugin"/>.
/// It adds an initialization step (with the plugin context) and an unload step to the start/stop the core
/// contract already defines, so the plugin manager can drive the full Load → Initialize → Start → Stop →
/// Unload → Reload sequence. Plugins that do not implement it still start and stop through <c>IPlugin</c>.
/// </summary>
public interface IPluginLifecycle
{
    /// <summary>Initializes the plugin with its runtime context, before it is started.</summary>
    /// <param name="context">The plugin context.</param>
    /// <param name="cancellationToken">A token to cancel initialization.</param>
    /// <returns>A task that completes when initialization has finished.</returns>
    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken);

    /// <summary>Unloads the plugin, releasing anything held beyond a stop, before it is removed or reloaded.</summary>
    /// <param name="cancellationToken">A token to cancel unloading.</param>
    /// <returns>A task that completes when unloading has finished.</returns>
    Task UnloadAsync(CancellationToken cancellationToken);
}
