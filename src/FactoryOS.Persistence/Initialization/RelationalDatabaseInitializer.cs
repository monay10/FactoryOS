using System.Text.RegularExpressions;
using FactoryOS.Persistence.Context;
using FactoryOS.Persistence.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Persistence.Initialization;

/// <summary>
/// Default <see cref="IDatabaseInitializer"/>. On PostgreSQL it creates the tenant schema before
/// applying migrations; on providers without schema support (or when migrations are disabled) it
/// creates the model directly.
/// </summary>
public sealed partial class RelationalDatabaseInitializer : IDatabaseInitializer
{
    private readonly ITenantSchemaProvider _schemaProvider;

    /// <summary>Initializes a new instance of the <see cref="RelationalDatabaseInitializer"/> class.</summary>
    /// <param name="schemaProvider">The tenant schema provider.</param>
    public RelationalDatabaseInitializer(ITenantSchemaProvider schemaProvider)
    {
        ArgumentNullException.ThrowIfNull(schemaProvider);
        _schemaProvider = schemaProvider;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(
        FactoryOsDbContext context,
        bool applyMigrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var isPostgres = context.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;

        if (applyMigrations && isPostgres)
        {
            var schema = _schemaProvider.Schema;
            if (!IdentifierRegex().IsMatch(schema))
            {
                throw new InvalidOperationException($"'{schema}' is not a valid schema identifier.");
            }

            // The schema name is a validated identifier (not a parameterizable value), so raw SQL is required.
#pragma warning disable EF1002
            await context.Database
                .ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", cancellationToken)
                .ConfigureAwait(false);
#pragma warning restore EF1002
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();
}
