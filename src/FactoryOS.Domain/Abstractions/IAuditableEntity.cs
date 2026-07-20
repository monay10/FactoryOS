namespace FactoryOS.Domain.Abstractions;

/// <summary>
/// Opt-in contract for entities that carry audit metadata. The persistence layer fills these values
/// automatically on insert and update.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>Gets the UTC instant the entity was created.</summary>
    DateTimeOffset CreatedOnUtc { get; }

    /// <summary>Gets the actor that created the entity, if known.</summary>
    string? CreatedBy { get; }

    /// <summary>Gets the UTC instant the entity was last modified, if ever.</summary>
    DateTimeOffset? ModifiedOnUtc { get; }

    /// <summary>Gets the actor that last modified the entity, if known.</summary>
    string? ModifiedBy { get; }

    /// <summary>Applies creation audit metadata.</summary>
    /// <param name="whenUtc">The creation instant.</param>
    /// <param name="actor">The creating actor, if known.</param>
    void ApplyCreated(DateTimeOffset whenUtc, string? actor);

    /// <summary>Applies modification audit metadata.</summary>
    /// <param name="whenUtc">The modification instant.</param>
    /// <param name="actor">The modifying actor, if known.</param>
    void ApplyModified(DateTimeOffset whenUtc, string? actor);
}
