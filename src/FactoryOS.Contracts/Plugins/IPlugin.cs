using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Contracts.Plugins;

/// <summary>
/// The code contract every plugin implements. A plugin is a self-contained feature that contributes
/// its own services and participates in the host lifecycle. Plugins communicate only through the
/// event bus and never reference one another directly.
/// </summary>
public interface IPlugin
{
    /// <summary>Gets the stable key identifying this plugin; it must match the manifest key.</summary>
    string Key { get; }

    /// <summary>Registers the plugin's services into the host container.</summary>
    /// <param name="services">The service collection to contribute registrations to.</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>Starts the plugin. Called after every enabled plugin's services are configured.</summary>
    /// <param name="cancellationToken">A token to cancel start-up.</param>
    /// <returns>A task that completes when start-up has finished.</returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stops the plugin, releasing any resources it holds.</summary>
    /// <param name="cancellationToken">A token to cancel shutdown.</param>
    /// <returns>A task that completes when shutdown has finished.</returns>
    Task StopAsync(CancellationToken cancellationToken);
}
