using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Activation;
using FactoryOS.Plugin.Catalog;
using FactoryOS.Plugin.Configuration;
using FactoryOS.Plugin.Discovery;
using FactoryOS.Plugin.Health;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugin.Loading;
using FactoryOS.Plugin.Management;
using FactoryOS.Plugin.Registry;
using FactoryOS.Plugin.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Plugin</b> framework, which discovers, loads
/// and manages the lifecycle of feature plugins.
/// </summary>
public static class PluginServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Plugin framework services into the dependency-injection container.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPluginFramework(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions();

        services.TryAddSingleton<IPluginRegistry, PluginRegistry>();
        services.TryAddSingleton<IPluginDiscovery, PluginDiscovery>();
        services.TryAddSingleton<IModuleLoader, ModuleLoader>();
        services.TryAddSingleton<IPluginHost, PluginHost>();

        // Foundation additions: activation, per-plugin configuration, health, catalog and the lifecycle manager.
        services.TryAddSingleton<IPluginActivator, PluginActivator>();
        services.TryAddSingleton<IPluginConfigurationProvider, PluginConfigurationProvider>();
        services.TryAddSingleton<IPluginHealthService, PluginHealthService>();
        services.TryAddSingleton<IPluginCatalog, PluginCatalog>();
        services.TryAddSingleton<IPluginManager, PluginManager>();

        return services;
    }

    /// <summary>
    /// Registers the Plugin framework and binds its options from the <c>Plugins</c> configuration section
    /// (including the nested <c>Discovery</c>, <c>Catalog</c> and <c>Health</c> sections).
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPluginFramework(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddPluginFramework();
        services.Configure<PluginOptions>(configuration.GetSection(PluginConstants.ConfigurationSection));

        return services;
    }

    /// <summary>
    /// Bootstraps the modular monolith: discovers every plugin under <paramref name="pluginsRoot"/>,
    /// loads and activates each one, lets it contribute its services, and registers a fully configured
    /// <see cref="IPluginHost"/>. A missing root or a failing plugin never aborts host start-up — the
    /// offending plugin is simply marked failed and skipped.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="pluginsRoot">The directory whose immediate subfolders each hold a plugin manifest.</param>
    /// <param name="loggerFactory">An optional logger factory used by the plugin host; a no-op logger is used when omitted.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPluginModules(
        this IServiceCollection services,
        string pluginsRoot,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsRoot);

        services.AddPluginFramework();

        var registry = new PluginRegistry();
        var loader = new ModuleLoader();
        var instances = new List<IPlugin>();

        foreach (var descriptor in new PluginDiscovery().Discover(pluginsRoot))
        {
            registry.Register(descriptor);

            if (descriptor.State == PluginState.Failed)
            {
                continue;
            }

            var load = loader.Load(descriptor);
            if (load.IsFailure)
            {
                descriptor.MarkFailed(load.Error.Description);
                continue;
            }

            instances.Add(load.Value);
            services.AddSingleton(load.Value);
        }

        ILogger<PluginHost> hostLogger = loggerFactory is null
            ? NullLogger<PluginHost>.Instance
            : new Logger<PluginHost>(loggerFactory);

        var host = new PluginHost(registry, instances, hostLogger);
        host.ConfigureServices(services);

        services.AddSingleton<IPluginRegistry>(registry);
        services.AddSingleton<IPluginHost>(host);

        return services;
    }
}
