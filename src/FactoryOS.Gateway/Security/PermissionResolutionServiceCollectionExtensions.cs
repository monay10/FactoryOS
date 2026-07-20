using FactoryOS.Gateway.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Service registration for gateway permission resolution.</summary>
public static class PermissionResolutionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the request-scoped <see cref="FactoryOS.Gateway.Security.IPermissionContext"/> and the default
    /// <see cref="FactoryOS.Gateway.Security.PermissionResolutionOptions"/>. Pair with <c>UsePermissionResolution</c>.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configure">An optional callback to customize the resolution options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPermissionResolution(
        this IServiceCollection services,
        Action<FactoryOS.Gateway.Security.PermissionResolutionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new FactoryOS.Gateway.Security.PermissionResolutionOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddScoped<FactoryOS.Gateway.Security.PermissionContext>();
        services.TryAddScoped<FactoryOS.Gateway.Security.IPermissionContext>(
            static sp => sp.GetRequiredService<FactoryOS.Gateway.Security.PermissionContext>());

        return services;
    }
}
