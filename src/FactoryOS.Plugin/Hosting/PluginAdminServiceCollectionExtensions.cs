using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugin.Hosting;

/// <summary>Registers the plugin administration (Store write-side) services.</summary>
public static class PluginAdminServiceCollectionExtensions
{
    /// <summary>Registers the default <see cref="IPluginAdmin"/> over the resolved <see cref="IPluginHost"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPluginAdministration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IPluginAdmin, PluginAdmin>();
        return services;
    }
}
