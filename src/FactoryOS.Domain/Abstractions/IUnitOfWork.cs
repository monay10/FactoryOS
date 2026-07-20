namespace FactoryOS.Domain.Abstractions;

/// <summary>Coordinates the atomic persistence of all changes made within a single business transaction.</summary>
public interface IUnitOfWork
{
    /// <summary>Persists all pending changes to the underlying store.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of state entries written to the store.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
