using FactoryOS.Identity.Authorization.Caching;
using FactoryOS.Identity.Authorization.Configuration;
using FactoryOS.Identity.Authorization.Context;
using FactoryOS.Identity.Authorization.Evaluation;
using FactoryOS.Identity.Authorization.Handlers;
using FactoryOS.Identity.Authorization.Services;
using FactoryOS.Identity.Context;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Authorization foundation</b> — the permission evaluator,
/// role service (inheritance), permission service (catalog and assignments), policy provider, authorization
/// handlers, cache and the authorization service. It reuses the existing <c>Permission</c> and
/// <c>AuthorizationPolicy</c> models and the identity context rather than duplicating them.
/// </summary>
public static class AuthorizationFoundationServiceCollectionExtensions
{
    /// <summary>Registers the authorization foundation and binds its configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddAuthorizationFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AuthorizationOptions>(
            configuration.GetSection(AuthorizationConstants.ConfigurationSection));

        // Reuse the identity context (idempotent) so the accessor is self-sufficient.
        services.TryAddScoped<IdentityContext>();

        services.TryAddSingleton<IAuthorizationCache, InMemoryAuthorizationCache>();
        services.TryAddSingleton<IPermissionEvaluator, PermissionEvaluator>();
        services.TryAddSingleton<IRoleService, RoleService>();
        services.TryAddSingleton<IPermissionService, PermissionService>();
        services.TryAddSingleton<IPolicyProvider, PolicyProvider>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, PermissionAuthorizationHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, RoleAuthorizationHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, PolicyAuthorizationHandler>());

        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<IAuthorizationContextAccessor, AuthorizationContextAccessor>();

        return services;
    }
}
