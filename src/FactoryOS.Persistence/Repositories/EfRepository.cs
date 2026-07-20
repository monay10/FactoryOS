using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Persistence.Repositories;

/// <summary>
/// A generic Entity Framework Core <see cref="IRepository{TAggregate, TId}"/>. Persistence of changes
/// is deferred to the <see cref="Domain.Abstractions.IUnitOfWork"/> so the aggregate stays the
/// transactional boundary.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TId">The aggregate identifier type.</typeparam>
public class EfRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>Initializes a new instance of the <see cref="EfRepository{TAggregate, TId}"/> class.</summary>
    /// <param name="context">The database context.</param>
    public EfRepository(DbContext context)
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
        return await Set.FirstOrDefaultAsync(aggregate => aggregate.Id.Equals(id), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        await Set.AddAsync(aggregate, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Update(TAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        Set.Update(aggregate);
    }

    /// <inheritdoc />
    public void Remove(TAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        Set.Remove(aggregate);
    }
}
