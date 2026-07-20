using FactoryOS.Identity.Authentication;
using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Credentials;
using FactoryOS.Identity.Persistence;
using FactoryOS.Identity.Seeding;
using FactoryOS.Identity.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Identity</b> layer (tenants, users, roles,
/// permissions, authentication and authorization).
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Identity layer services into the dependency-injection container.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root (binds <see cref="JwtOptions"/>).</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.TryAddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.TryAddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.TryAddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
        services.TryAddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.TryAddSingleton<IPermissionAuthorizer, PermissionAuthorizer>();
        services.TryAddSingleton<IUserStore, InMemoryUserStore>();
        services.TryAddSingleton<IRoleStore, InMemoryRoleStore>();
        services.TryAddSingleton<IAuthenticator, Authenticator>();

        services.Configure<IdentitySeedOptions>(configuration.GetSection(IdentitySeedOptions.SectionName));
        services.TryAddSingleton<DefaultIdentitySeeder>();

        return services;
    }
}
