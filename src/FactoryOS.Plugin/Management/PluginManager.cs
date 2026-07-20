using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Results;
using FactoryOS.Plugin.Configuration;
using FactoryOS.Plugin.Health;
using FactoryOS.Plugin.Lifecycle;
using FactoryOS.Plugin.Registry;
using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Plugin.Management;

/// <summary>
/// Drives a single plugin through its lifecycle — Initialize, Start, Stop, Unload and Reload — over the
/// registry descriptors, honouring the optional <see cref="IPluginLifecycle"/> and recording heartbeats
/// with the health service. In the first-party modular monolith an instance stays in memory across
/// stop/unload; Reload is an in-process stop-then-start and no external assembly is reloaded.
/// </summary>
public interface IPluginManager
{
    /// <summary>Gets the descriptors of all known plugins.</summary>
    IReadOnlyCollection<PluginDescriptor> Plugins { get; }

    /// <summary>Initializes a plugin, invoking its extended lifecycle hook when it implements one.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure describing why the plugin could not be initialized.</returns>
    Task<Result> InitializeAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Starts a plugin and records its first heartbeat.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> StartAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Stops a started plugin, returning it to the loaded state.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> StopAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Stops (if running) and unloads a plugin, returning it to the discovered state.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> UnloadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Reloads a plugin in process: stops it, then starts it again.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> ReloadAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IPluginManager"/>.</summary>
public sealed class PluginManager : IPluginManager
{
    private readonly IPluginRegistry _registry;
    private readonly IPluginConfigurationProvider _configuration;
    private readonly IPluginHealthService _health;

    /// <summary>Initializes a new instance of the <see cref="PluginManager"/> class.</summary>
    /// <param name="registry">The plugin registry.</param>
    /// <param name="configuration">The plugin configuration provider.</param>
    /// <param name="health">The plugin health service.</param>
    public PluginManager(
        IPluginRegistry registry, IPluginConfigurationProvider configuration, IPluginHealthService health)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(health);

        _registry = registry;
        _configuration = configuration;
        _health = health;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PluginDescriptor> Plugins => _registry.All;

    /// <inheritdoc />
    public async Task<Result> InitializeAsync(string key, CancellationToken cancellationToken = default)
    {
        var resolved = Resolve(key);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        var (descriptor, instance) = resolved.Value;
        if (instance is IPluginLifecycle lifecycle)
        {
            var context = new PluginContext(descriptor, _configuration.GetConfiguration(key));
            await lifecycle.InitializeAsync(context, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> StartAsync(string key, CancellationToken cancellationToken = default)
    {
        var resolved = Resolve(key);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        var (descriptor, instance) = resolved.Value;
        await instance.StartAsync(cancellationToken).ConfigureAwait(false);
        descriptor.MarkStarted();
        _health.Heartbeat(key);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> StopAsync(string key, CancellationToken cancellationToken = default)
    {
        var resolved = Resolve(key);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        var (descriptor, instance) = resolved.Value;
        await instance.StopAsync(cancellationToken).ConfigureAwait(false);
        descriptor.MarkStopped();
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> UnloadAsync(string key, CancellationToken cancellationToken = default)
    {
        var resolved = Resolve(key);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        var (descriptor, instance) = resolved.Value;
        if (descriptor.State == PluginState.Started)
        {
            await instance.StopAsync(cancellationToken).ConfigureAwait(false);
            descriptor.MarkStopped();
        }

        if (instance is IPluginLifecycle lifecycle)
        {
            await lifecycle.UnloadAsync(cancellationToken).ConfigureAwait(false);
        }

        descriptor.MarkDiscovered();
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ReloadAsync(string key, CancellationToken cancellationToken = default)
    {
        var stop = await StopAsync(key, cancellationToken).ConfigureAwait(false);
        return stop.IsFailure ? stop : await StartAsync(key, cancellationToken).ConfigureAwait(false);
    }

    private Result<(PluginDescriptor Descriptor, IPlugin Instance)> Resolve(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var descriptor = _registry.Find(key);
        if (descriptor is null)
        {
            return Result.Failure<(PluginDescriptor, IPlugin)>(
                Error.NotFound("Plugin.Manager.NotFound", $"No plugin with key '{key}' is registered."));
        }

        if (descriptor.Instance is null)
        {
            return Result.Failure<(PluginDescriptor, IPlugin)>(Error.Validation(
                "Plugin.Manager.NotLoaded", $"Plugin '{key}' has no loaded instance to manage."));
        }

        return (descriptor, descriptor.Instance);
    }
}
