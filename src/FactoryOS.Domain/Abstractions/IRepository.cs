using FactoryOS.Domain.Primitives;

namespace FactoryOS.Domain.Abstractions;

/// <summary>
/// Persistence abstraction for an aggregate root. Only aggregate roots are retrieved and stored,
/// preserving the aggregate as the transactional consistency boundary.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TId">The aggregate identifier type.</typeparam>
public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>Retrieves an aggregate by its identifier.</summary>
    /// <param name="id">The identifier of the aggregate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The aggregate, or <see langword="null"/> when not found.</returns>
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Adds a new aggregate to the repository.</summary>
    /// <param name="aggregate">The aggregate to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the aggregate has been added.</returns>
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>Marks an existing aggregate as modified.</summary>
    /// <param name="aggregate">The aggregate to update.</param>
    void Update(TAggregate aggregate);

    /// <summary>Removes an aggregate from the repository.</summary>
    /// <param name="aggregate">The aggregate to remove.</param>
    void Remove(TAggregate aggregate);
}
