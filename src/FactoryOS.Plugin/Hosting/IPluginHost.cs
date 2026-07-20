using FactoryOS.Plugin.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Plugin.Hosting;

/// <summary>
/// Orchestrates the plugin lifecycle: it resolves the load order from declared dependencies,
/// configures each enabled plugin's services, and starts and stops plugins in the correct order.
/// </summary>
public interface IPluginHost
{
    /// <summary>Gets the descriptors of all known plugins.</summary>
    IReadOnlyCollection<PluginDescriptor> Plugins { get; }

    /// <summary>Gets the enabled plugins in resolved load order, available after <see cref="ConfigureServices"/>.</summary>
    IReadOnlyList<PluginDescriptor> LoadOrder { get; }

    /// <summary>Resolves the load order and lets each enabled plugin contribute its services.</summary>
    /// <param name="services">The host service collection.</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>Starts every loaded plugin in dependency-first order.</summary>
    /// <param name="cancellationToken">A token to cancel start-up.</param>
    /// <returns>A task that completes when all plugins have started.</returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stops every started plugin in reverse dependency order.</summary>
    /// <param name="cancellationToken">A token to cancel shutdown.</param>
    /// <returns>A task that completes when all plugins have stopped.</returns>
    Task StopAsync(CancellationToken cancellationToken);
}
