namespace FactoryOS.Domain.Primitives;

/// <summary>
/// Base class for aggregate roots — the only entities that repositories load and persist, and the
/// consistency boundary within which invariants are enforced.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    /// <summary>Initializes a new instance of the <see cref="AggregateRoot{TId}"/> class.</summary>
    /// <param name="id">The unique identifier of the aggregate.</param>
    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AggregateRoot{TId}"/> class for the ORM.</summary>
    protected AggregateRoot()
    {
    }
}
