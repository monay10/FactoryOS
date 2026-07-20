namespace FactoryOS.Domain.Primitives;

/// <summary>
/// Base class for domain entities identified by <typeparamref name="TId"/>. Two entities are equal
/// when they are of the same runtime type and share the same identifier.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
public abstract class Entity<TId> : BaseEntity, IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>Initializes a new instance of the <see cref="Entity{TId}"/> class.</summary>
    /// <param name="id">The unique identifier of the entity.</param>
    protected Entity(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    /// <summary>Initializes a new instance of the <see cref="Entity{TId}"/> class for the ORM.</summary>
    protected Entity()
    {
    }

    /// <summary>Gets the unique identifier of the entity.</summary>
    public TId Id { get; protected init; } = default!;

    /// <summary>Determines whether two entities are equal.</summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns><see langword="true"/> if the entities are equal; otherwise <see langword="false"/>.</returns>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }

    /// <summary>Determines whether two entities are not equal.</summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns><see langword="true"/> if the entities differ; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }

    /// <inheritdoc />
    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return GetType() == other.GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> entity && Equals(entity);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }
}
