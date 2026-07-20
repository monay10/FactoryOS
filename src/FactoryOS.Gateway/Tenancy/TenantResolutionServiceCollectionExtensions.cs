using FactoryOS.Gateway.Tenancy;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Service registration for gateway tenant resolution.</summary>
public static class TenantResolutionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the request-scoped <see cref="ITenantContext"/> and the default
    /// <see cref="TenantResolutionOptions"/>. Pair with <c>UseTenantResolution</c> in the pipeline.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configure">An optional callback to customize the resolution options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddTenantResolution(
        this IServiceCollection services,
        Action<TenantResolutionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new TenantResolutionOptions();
        configure?.Invoke(options);

        // Options are registered last-wins so an explicit AddTenantResolution(configure) after the
        // gateway's default registration overrides it; the scoped context is registered once.
        services.AddSingleton(options);
        services.TryAddScoped<TenantContext>();
        services.TryAddScoped<ITenantContext>(static sp => sp.GetRequiredService<TenantContext>());

        return services;
    }
}
