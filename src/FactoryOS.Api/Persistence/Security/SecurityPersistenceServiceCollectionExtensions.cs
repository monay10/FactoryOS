using FactoryOS.Api.Persistence.Security;
using FactoryOS.Persistence.Configuration;
using FactoryOS.Persistence.Multitenancy;
using FactoryOS.Plugins.Workflow.Security.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the EF Core persistence for the security engine's direct authorization grants — but only when a
/// database is configured. It follows the same gating pattern the plugin runtime and audit trail established: the
/// engine registers its <see cref="ISecurityRepository"/> through <c>TryAdd</c>, so a host that calls this
/// <b>before</b> <c>AddSecurityEngine</c> overrides the in-memory default; a host with no <c>Persistence</c> section
/// keeps the in-memory repository and needs no database to run.
/// </summary>
public static class SecurityPersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EfSecurityRepository"/> as the security engine's repository when a database is
    /// configured (a <c>Persistence</c> configuration section with a connection string); otherwise does nothing.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration, read for the persistence options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddSecurityPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(PersistenceConstants.ConfigurationSection);
        if (!section.Exists())
        {
            // No database configured — the engine keeps its in-memory repository, and the host runs without one.
            return services;
        }

        var options = new PersistenceOptions();
        section.Bind(options);
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return services;
        }

        services.TryAddSingleton<ITenantSchemaProvider>(
            _ => new FixedTenantSchemaProvider(PersistenceConstants.DefaultSchema));
        services.AddDbContextFactory<SecurityDbContext>(
            builder => builder.UseFactoryOsDatabase<SecurityDbContext>(options));

        // Registered as a plain singleton (not TryAdd) so it wins over the engine's in-memory default, which is
        // registered with TryAdd. This method must therefore run before AddSecurityEngine.
        services.AddSingleton<ISecurityRepository, EfSecurityRepository>();

        return services;
    }
}
