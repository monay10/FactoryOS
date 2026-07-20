using FactoryOS.Domain.Abstractions;
using FactoryOS.Persistence.Auditing;
using FactoryOS.Persistence.Configuration;
using FactoryOS.Persistence.Context;
using FactoryOS.Persistence.Migrations;
using FactoryOS.Persistence.Repositories;
using FactoryOS.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Per-context registration for the FactoryOS persistence foundation. A module calls
/// <see cref="AddFactoryOsDbContext{TContext}"/> with its own <see cref="FactoryOsDbContext"/>-derived context; this
/// wires the provider (from <see cref="PersistenceOptions"/>), the auditing interceptor, the bridge to the base
/// <see cref="DbContext"/> service, and the generic repositories and unit of work. Persistence itself owns no
/// business context, so the context type is always supplied by the caller.
/// </summary>
public static class PersistenceContextRegistrationExtensions
{
    /// <summary>Registers a FactoryOS context and its repositories, unit of work and interceptors.</summary>
    /// <typeparam name="TContext">The concrete <see cref="FactoryOsDbContext"/>-derived context.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The persistence options selecting and tuning the provider.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddFactoryOsDbContext<TContext>(
        this IServiceCollection services,
        PersistenceOptions options)
        where TContext : FactoryOsDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddDbContext<TContext>((provider, builder) =>
        {
            builder.UseFactoryOsDatabase<TContext>(options);
            builder.AddInterceptors(provider.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        // Bridge the base DbContext service to this context so the generic repositories and unit of work resolve.
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<TContext>());

        services.TryAddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));
        services.TryAddScoped(typeof(IReadRepository<,>), typeof(ReadRepository<,>));
        services.TryAddScoped<IUnitOfWork, EfUnitOfWork>();
        services.TryAddScoped<FactoryOS.Shared.Abstractions.IUnitOfWork, EfUnitOfWork>();

        return services;
    }

    /// <summary>Configures a <see cref="DbContextOptionsBuilder"/> for the FactoryOS provider selected by options.</summary>
    /// <typeparam name="TContext">The context type (used to resolve the migrations assembly).</typeparam>
    /// <param name="builder">The context options builder.</param>
    /// <param name="options">The persistence options.</param>
    /// <returns>The same <see cref="DbContextOptionsBuilder"/> instance, to allow chaining.</returns>
    public static DbContextOptionsBuilder UseFactoryOsDatabase<TContext>(
        this DbContextOptionsBuilder builder,
        PersistenceOptions options)
        where TContext : FactoryOsDbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        var migrationsAssembly = MigrationAssemblyResolver.Resolve(typeof(TContext), options);

        switch (options.Provider)
        {
            case DatabaseProvider.PostgreSql:
                builder.UseNpgsql(options.ConnectionString, npgsql =>
                {
                    npgsql.CommandTimeout(options.CommandTimeoutSeconds);
                    npgsql.MigrationsAssembly(migrationsAssembly);
                    npgsql.MigrationsHistoryTable(PersistenceConstants.MigrationsHistoryTable);
                    if (options.MaxRetryCount > 0)
                    {
                        npgsql.EnableRetryOnFailure(options.MaxRetryCount);
                    }
                });
                break;

            case DatabaseProvider.Sqlite:
            default:
                builder.UseSqlite(options.ConnectionString, sqlite =>
                {
                    sqlite.CommandTimeout(options.CommandTimeoutSeconds);
                    sqlite.MigrationsAssembly(migrationsAssembly);
                    sqlite.MigrationsHistoryTable(PersistenceConstants.MigrationsHistoryTable);
                });
                break;
        }

        if (options.EnableDetailedErrors)
        {
            builder.EnableDetailedErrors();
        }

        if (options.EnableSensitiveDataLogging)
        {
            builder.EnableSensitiveDataLogging();
        }

        return builder;
    }
}
