using FactoryOS.Persistence.Configuration;
using FactoryOS.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Persistence.Migrations;

/// <summary>Resolves the assembly EF Core should look in for a context's migrations.</summary>
public static class MigrationAssemblyResolver
{
    /// <summary>
    /// Resolves the migrations-assembly name for a context: the explicit <see cref="PersistenceOptions.MigrationsAssembly"/>
    /// when configured, otherwise the assembly that declares the context type (migrations live beside their context).
    /// </summary>
    /// <param name="contextType">The context type.</param>
    /// <param name="options">The persistence options.</param>
    /// <returns>The migrations-assembly name.</returns>
    public static string Resolve(Type contextType, PersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(contextType);
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.MigrationsAssembly))
        {
            return options.MigrationsAssembly;
        }

        return contextType.Assembly.GetName().Name
            ?? throw new InvalidOperationException($"The assembly of '{contextType.Name}' has no name.");
    }
}

/// <summary>Applies and inspects EF Core migrations for a FactoryOS context.</summary>
public interface IDatabaseMigrator
{
    /// <summary>Applies all pending migrations to the context's database.</summary>
    /// <param name="context">The context to migrate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the database is migrated.</returns>
    Task MigrateAsync(FactoryOsDbContext context, CancellationToken cancellationToken = default);

    /// <summary>Lists the migrations that have not yet been applied to the context's database.</summary>
    /// <param name="context">The context to inspect.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The pending migration names.</returns>
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        FactoryOsDbContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>The default <see cref="IDatabaseMigrator"/>, applying migrations through the relational provider.</summary>
public sealed class DatabaseMigrator : IDatabaseMigrator
{
    /// <inheritdoc />
    public async Task MigrateAsync(FactoryOsDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        FactoryOsDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        return [.. pending];
    }
}
