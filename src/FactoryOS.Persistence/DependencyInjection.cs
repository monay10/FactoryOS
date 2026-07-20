using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Persistence.Auditing;
using FactoryOS.Persistence.Initialization;
using FactoryOS.Persistence.Multitenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Persistence</b> layer (PostgreSQL / EF Core,
/// repositories, unit of work, auditing, soft delete, concurrency and tenant-scoped data access).
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cross-cutting persistence services: the auditing interceptor, the current-actor
    /// and tenant-schema providers, and the database initializer. Module-specific
    /// <see cref="FactoryOS.Persistence.Context.FactoryOsDbContext"/> instances are registered by the
    /// modules that own them.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root (connection strings, options).</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.TryAddSingleton<ICurrentActorProvider, SystemActorProvider>();
        services.TryAddSingleton<ITenantSchemaProvider>(_ => new FixedTenantSchemaProvider("public"));
        services.TryAddScoped<AuditingSaveChangesInterceptor>();
        services.TryAddScoped<IDatabaseInitializer, RelationalDatabaseInitializer>();

        return services;
    }
}
