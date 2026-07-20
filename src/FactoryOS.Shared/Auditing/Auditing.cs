namespace FactoryOS.Shared.Auditing;

/// <summary>
/// Immutable creation and last-modification audit stamps for an entity. Value equality. Start from
/// <see cref="Created"/> and evolve with <see cref="Modified"/>.
/// </summary>
/// <param name="CreatedAt">When the entity was created.</param>
/// <param name="CreatedBy">Who created it (a user name or system actor), when known.</param>
/// <param name="ModifiedAt">When the entity was last modified, if ever.</param>
/// <param name="ModifiedBy">Who last modified it, when known.</param>
public sealed record AuditInfo(
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy)
{
    /// <summary>Creates an audit stamp for a newly created entity.</summary>
    /// <param name="at">When the entity was created.</param>
    /// <param name="by">Who created it, when known.</param>
    /// <returns>A new <see cref="AuditInfo"/> with no modification recorded.</returns>
    public static AuditInfo Created(DateTimeOffset at, string? by = null) => new(at, by, null, null);

    /// <summary>Returns a copy stamped with a modification.</summary>
    /// <param name="at">When the modification occurred.</param>
    /// <param name="by">Who modified it, when known.</param>
    /// <returns>A new <see cref="AuditInfo"/> carrying the modification.</returns>
    public AuditInfo Modified(DateTimeOffset at, string? by = null) => this with { ModifiedAt = at, ModifiedBy = by };
}

/// <summary>Marks an entity that carries <see cref="AuditInfo"/>.</summary>
public interface IAuditable
{
    /// <summary>Gets the entity's audit stamps.</summary>
    AuditInfo Audit { get; }
}

/// <summary>Marks an entity that is soft-deleted (hidden, not physically removed).</summary>
public interface ISoftDelete
{
    /// <summary>Gets a value indicating whether the entity is deleted.</summary>
    bool IsDeleted { get; }

    /// <summary>Gets when the entity was deleted, if it has been.</summary>
    DateTimeOffset? DeletedAt { get; }
}

/// <summary>Marks an entity guarded by an optimistic-concurrency token.</summary>
public interface IHasConcurrencyToken
{
    /// <summary>Gets the opaque concurrency token compared on update to detect concurrent modification.</summary>
    string? ConcurrencyToken { get; }
}
