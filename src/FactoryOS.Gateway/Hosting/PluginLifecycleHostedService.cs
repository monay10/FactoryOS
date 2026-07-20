using FactoryOS.Plugin.Hosting;
using Microsoft.Extensions.Hosting;

namespace FactoryOS.Gateway.Hosting;

/// <summary>
/// Drives the plugin host's lifecycle from the application's own lifecycle: every configured plugin is
/// started when the application starts and stopped, in reverse order, when it shuts down.
/// </summary>
public sealed class PluginLifecycleHostedService : IHostedService
{
    private readonly IPluginHost _host;

    /// <summary>Initializes a new instance of the <see cref="PluginLifecycleHostedService"/> class.</summary>
    /// <param name="host">The plugin host to start and stop.</param>
    public PluginLifecycleHostedService(IPluginHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => _host.StartAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => _host.StopAsync(cancellationToken);
}
