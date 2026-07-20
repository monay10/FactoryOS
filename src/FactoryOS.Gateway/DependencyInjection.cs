using FactoryOS.Gateway.Branding;
using FactoryOS.Gateway.Hosting;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>API Gateway</b> — the module loader's web surface. It
/// exposes the module inventory and UI registry and drives the plugin lifecycle from the host.
/// </summary>
public static class GatewayServiceCollectionExtensions
{
    /// <summary>
    /// Registers the gateway's UI catalog provider and the hosted service that starts and stops plugins
    /// with the application. Call after the plugin host has been configured (for example via
    /// <c>AddPluginModules</c>).
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddModuleGateway(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        // Neutral by default; a host overrides with a provider seeded from tenant configuration (Law 6).
        services.TryAddSingleton<ITenantBrandingProvider>(new TenantBrandingProvider());
        services.AddPluginAdministration();
        services.AddTenantResolution();
        services.AddPermissionResolution();
        services.AddHostedService<PluginLifecycleHostedService>();

        return services;
    }
}
