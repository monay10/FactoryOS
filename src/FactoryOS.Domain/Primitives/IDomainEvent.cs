namespace FactoryOS.Domain.Primitives;

/// <summary>
/// Contract for a domain event — an immutable fact describing something significant that has
/// happened inside the domain and to which other parts of the system may react.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Gets the unique identifier of this domain event occurrence.</summary>
    Guid EventId { get; }

    /// <summary>Gets the UTC instant at which the event occurred.</summary>
    DateTimeOffset OccurredOnUtc { get; }
}
