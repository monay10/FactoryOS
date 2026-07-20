using FactoryOS.Persistence.Context;

namespace FactoryOS.Persistence.Initialization;

/// <summary>Prepares a context's database: ensures the tenant schema exists and applies the schema/migrations.</summary>
public interface IDatabaseInitializer
{
    /// <summary>Initializes the database for a context.</summary>
    /// <param name="context">The context to initialize.</param>
    /// <param name="applyMigrations">
    /// When <see langword="true"/> the tenant schema is created and EF Core migrations are applied;
    /// when <see langword="false"/> the schema is created from the model (development/tests).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the database is ready.</returns>
    Task InitializeAsync(
        FactoryOsDbContext context,
        bool applyMigrations,
        CancellationToken cancellationToken = default);
}
