using FactoryOS.Infrastructure.PluginRuntime;
using FactoryOS.Persistence.Configuration;
using FactoryOS.Persistence.Multitenancy;
using FactoryOS.Plugins.Runtime.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the EF Core persistence for the plugin runtime — but only when a database is configured. This is the
/// gating pattern future engine persistence follows: the runtime registers its stores through <c>TryAdd</c>, so a
/// host that calls this <b>before</b> <c>AddPluginRuntime</c> overrides the in-memory default; a host with no
/// <c>Persistence</c> section keeps the in-memory store and needs no database to run.
/// </summary>
public static class PluginRuntimePersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EfPluginStore"/> as the plugin runtime's store when a database is configured
    /// (a <c>Persistence</c> configuration section with a connection string); otherwise does nothing.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration, read for the persistence options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPluginRuntimePersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(PersistenceConstants.ConfigurationSection);
        if (!section.Exists())
        {
            // No database configured — the runtime keeps its in-memory store, and the host runs without one.
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
        services.AddDbContextFactory<PluginRuntimeDbContext>(
            builder => builder.UseFactoryOsDatabase<PluginRuntimeDbContext>(options));

        // Registered as a plain singleton (not TryAdd) so it wins over the runtime's in-memory default, which is
        // registered with TryAdd. This method must therefore run before AddPluginRuntime.
        services.AddSingleton<IPluginStore, EfPluginStore>();

        return services;
    }
}
