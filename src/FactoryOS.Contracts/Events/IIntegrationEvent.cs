namespace FactoryOS.Contracts.Events;

/// <summary>
/// Contract for an integration event — an immutable fact published on the event bus for other
/// modules to react to. Every event carries a stable identity so consumers can deduplicate.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Gets the stable, unique identifier of this event instance (used for idempotency).</summary>
    Guid EventId { get; }

    /// <summary>Gets the UTC instant at which the event occurred.</summary>
    DateTimeOffset OccurredOnUtc { get; }
}
