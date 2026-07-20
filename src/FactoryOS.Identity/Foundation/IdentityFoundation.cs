using FactoryOS.Identity.Configuration;
using FactoryOS.Identity.Context;
using FactoryOS.Identity.Execution;
using FactoryOS.Identity.Lockout;
using FactoryOS.Identity.Policies;
using FactoryOS.Identity.Services;
using FactoryOS.Identity.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Identity foundation</b> — the ambient identity context,
/// current-principal and current-claims accessors, password policy, account lockout, sessions and the
/// identity façade. It composes (and does not replace) the base identity module registered by
/// <see cref="IdentityServiceCollectionExtensions.AddIdentityModule"/>.
/// </summary>
public static class IdentityFoundationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full Identity foundation: binds the identity, password-policy and session
    /// configuration sections and adds the foundation services on top of the base identity module.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddIdentityFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Reuse the base module (idempotent via TryAdd) so the foundation is self-sufficient.
        services.AddIdentityModule(configuration);

        services.Configure<IdentityOptions>(configuration.GetSection(IdentityConstants.ConfigurationSection));
        services.Configure<PasswordPolicyOptions>(
            configuration.GetSection(IdentityConstants.PasswordPolicySection));
        services.Configure<SessionOptions>(configuration.GetSection(IdentityConstants.SessionSection));

        services.TryAddScoped<IdentityContext>();
        services.TryAddScoped<ICurrentPrincipalAccessor, CurrentPrincipalAccessor>();
        services.TryAddScoped<ICurrentClaimsAccessor, CurrentClaimsAccessor>();

        services.TryAddSingleton<IPasswordPolicy, PasswordPolicyValidator>();

        services.TryAddSingleton<ILoginAttemptStore, InMemoryLoginAttemptStore>();
        services.TryAddSingleton<IAccountLockoutService, AccountLockoutService>();

        services.TryAddSingleton<ISessionStore, InMemorySessionStore>();
        services.TryAddSingleton<ISessionService, SessionService>();

        services.TryAddScoped<IIdentityService, IdentityService>();

        return services;
    }
}
