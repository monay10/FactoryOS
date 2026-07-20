using System.Linq.Expressions;
using FactoryOS.Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Persistence.Repositories;

/// <summary>A read-only view over an aggregate's set: every query is tracking-free.</summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TId">The aggregate identifier type.</typeparam>
public interface IReadRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>Retrieves an aggregate by its identifier, without tracking.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The aggregate, or <see langword="null"/> when not found.</returns>
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Lists all aggregates, without tracking.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The aggregates.</returns>
    Task<IReadOnlyList<TAggregate>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the aggregates matching a predicate, without tracking.</summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching aggregates.</returns>
    Task<IReadOnlyList<TAggregate>> ListAsync(
        Expression<Func<TAggregate, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>Counts the aggregates matching a predicate.</summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The count.</returns>
    Task<int> CountAsync(
        Expression<Func<TAggregate, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>Determines whether any aggregate matches a predicate.</summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when at least one matches.</returns>
    Task<bool> AnyAsync(
        Expression<Func<TAggregate, bool>> predicate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The default Entity Framework Core <see cref="IReadRepository{TAggregate, TId}"/>. Queries run with
/// <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/> so read paths never pollute the change
/// tracker, keeping reads cheap and side-effect free.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TId">The aggregate identifier type.</typeparam>
public class ReadRepository<TAggregate, TId> : IReadRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>Initializes a new instance of the <see cref="ReadRepository{TAggregate, TId}"/> class.</summary>
    /// <param name="context">The database context.</param>
    public ReadRepository(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        Set = context.Set<TAggregate>();
    }

    /// <summary>Gets the database context.</summary>
    protected DbContext Context { get; }

    /// <summary>Gets the aggregate's entity set.</summary>
    protected DbSet<TAggregate> Set { get; }

    /// <inheritdoc />
    public async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return await Set.AsNoTracking()
            .FirstOrDefaultAsync(aggregate => aggregate.Id.Equals(id), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TAggregate>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Set.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TAggregate>> ListAsync(
        Expression<Func<TAggregate, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return await Set.AsNoTracking().Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        Expression<Func<TAggregate, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return await Set.AsNoTracking().CountAsync(predicate, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(
        Expression<Func<TAggregate, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return await Set.AsNoTracking().AnyAsync(predicate, cancellationToken).ConfigureAwait(false);
    }
}
