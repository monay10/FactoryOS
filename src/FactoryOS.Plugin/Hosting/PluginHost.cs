using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Dependencies;
using FactoryOS.Plugin.Registry;
using FactoryOS.Plugin.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FactoryOS.Plugin.Hosting;

/// <summary>
/// Default <see cref="IPluginHost"/> for the first-party modular monolith: plugin instances are
/// supplied by the container and matched to registered descriptors by key. Disabled plugins are
/// skipped; a descriptor with no matching instance is marked failed rather than aborting the host.
/// </summary>
public sealed class PluginHost : IPluginHost
{
    private readonly IPluginRegistry _registry;
    private readonly Dictionary<string, IPlugin> _instances;
    private readonly ILogger<PluginHost> _logger;
    private PluginDescriptor[] _loadOrder = [];

    /// <summary>Initializes a new instance of the <see cref="PluginHost"/> class.</summary>
    /// <param name="registry">The plugin registry (source of descriptors).</param>
    /// <param name="plugins">The compiled plugin instances resolved from the container.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentException">Thrown when two plugin instances share a key.</exception>
    public PluginHost(IPluginRegistry registry, IEnumerable<IPlugin> plugins, ILogger<PluginHost> logger)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentNullException.ThrowIfNull(logger);

        _registry = registry;
        _logger = logger;
        _instances = plugins.ToDictionary(plugin => plugin.Key, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PluginDescriptor> Plugins => _registry.All;

    /// <inheritdoc />
    public IReadOnlyList<PluginDescriptor> LoadOrder => _loadOrder;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _loadOrder = ResolveLoadOrder();

        for (var index = 0; index < _loadOrder.Length; index++)
        {
            var descriptor = _loadOrder[index];
            var instance = _instances[descriptor.Key];

            descriptor.AttachInstance(instance);
            instance.ConfigureServices(services);
            PluginLog.Configured(_logger, descriptor.Key, index);
        }
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var descriptor in _loadOrder)
        {
            await descriptor.Instance!.StartAsync(cancellationToken).ConfigureAwait(false);
            descriptor.MarkStarted();
            PluginLog.Started(_logger, descriptor.Key);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        for (var index = _loadOrder.Length - 1; index >= 0; index--)
        {
            var descriptor = _loadOrder[index];
            await descriptor.Instance!.StopAsync(cancellationToken).ConfigureAwait(false);
            PluginLog.Stopped(_logger, descriptor.Key);
        }
    }

    private PluginDescriptor[] ResolveLoadOrder()
    {
        var candidates = new Dictionary<string, PluginDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in _registry.All)
        {
            if (descriptor.State == PluginState.Disabled)
            {
                PluginLog.Skipped(_logger, descriptor.Key, "disabled");
                continue;
            }

            if (!_instances.ContainsKey(descriptor.Key))
            {
                descriptor.MarkFailed("No IPlugin instance is registered for this plugin.");
                PluginLog.Skipped(_logger, descriptor.Key, "no registered instance");
                continue;
            }

            candidates[descriptor.Key] = descriptor;
        }

        var manifests = candidates.Values.Select(descriptor => descriptor.Manifest).ToArray();
        var resolution = PluginDependencyResolver.Resolve(manifests);
        if (resolution.IsFailure)
        {
            throw new InvalidOperationException(
                $"Plugin dependency resolution failed ({resolution.Error.Code}): {resolution.Error.Description}");
        }

        return resolution.Value.Select(manifest => candidates[manifest.Key]).ToArray();
    }
}
