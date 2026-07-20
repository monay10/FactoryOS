namespace FactoryOS.Shared.Abstractions;

/// <summary>
/// Coordinates the atomic persistence of a set of changes. Implementations belong to the persistence layer; this
/// abstraction lives in the shared kernel so inner layers can depend on it without knowing the store.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all pending changes as a single atomic unit.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of state entries written to the store.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
