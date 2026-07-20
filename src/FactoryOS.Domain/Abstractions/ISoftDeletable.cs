namespace FactoryOS.Domain.Abstractions;

/// <summary>
/// Opt-in contract for entities that are soft-deleted: removal flips a flag instead of erasing the
/// row, and the persistence layer filters deleted rows out of queries automatically.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>Gets a value indicating whether the entity is soft-deleted.</summary>
    bool IsDeleted { get; }

    /// <summary>Gets the UTC instant the entity was soft-deleted, if it is.</summary>
    DateTimeOffset? DeletedOnUtc { get; }

    /// <summary>Gets the actor that soft-deleted the entity, if known.</summary>
    string? DeletedBy { get; }

    /// <summary>Marks the entity as soft-deleted.</summary>
    /// <param name="whenUtc">The deletion instant.</param>
    /// <param name="actor">The deleting actor, if known.</param>
    void ApplyDeleted(DateTimeOffset whenUtc, string? actor);

    /// <summary>Restores a soft-deleted entity.</summary>
    void Restore();
}
