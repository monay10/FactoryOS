namespace FactoryOS.Contracts.Events;

/// <summary>
/// Ambient metadata handed to an event handler for a single delivery attempt, carrying correlation
/// and tracing identifiers so handlers can log and act idempotently.
/// </summary>
/// <param name="MessageId">The unique identifier of this bus message (one per publish).</param>
/// <param name="EventId">The stable identifier of the event instance (used for deduplication).</param>
/// <param name="CorrelationId">The identifier correlating all messages of one logical operation.</param>
/// <param name="CausationId">The identifier of the message that caused this one, when applicable.</param>
/// <param name="TraceId">The distributed-tracing identifier of the ambient activity.</param>
/// <param name="Priority">The priority the event was published with.</param>
/// <param name="Attempt">The 1-based delivery attempt number for the current handler.</param>
/// <param name="OccurredOnUtc">The UTC instant at which the originating event occurred.</param>
public sealed record EventContext(
    Guid MessageId,
    Guid EventId,
    Guid CorrelationId,
    Guid? CausationId,
    string TraceId,
    EventPriority Priority,
    int Attempt,
    DateTimeOffset OccurredOnUtc);
