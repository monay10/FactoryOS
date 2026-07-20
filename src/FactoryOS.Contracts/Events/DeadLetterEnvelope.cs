namespace FactoryOS.Contracts.Events;

/// <summary>
/// A message that could not be handled after exhausting all retries, captured for inspection,
/// alerting and manual replay.
/// </summary>
/// <param name="MessageId">The unique identifier of the failed bus message.</param>
/// <param name="EventId">The stable identifier of the event instance.</param>
/// <param name="EventType">The CLR type name of the event.</param>
/// <param name="CorrelationId">The correlation identifier of the logical operation.</param>
/// <param name="CausationId">The identifier of the causing message, when applicable.</param>
/// <param name="TraceId">The distributed-tracing identifier.</param>
/// <param name="Priority">The priority the event was published with.</param>
/// <param name="Attempts">The number of attempts made before dead-lettering.</param>
/// <param name="FailureReason">The message of the final exception that caused the failure.</param>
/// <param name="Event">The original event instance.</param>
/// <param name="DeadLetteredOnUtc">The UTC instant at which the message was dead-lettered.</param>
public sealed record DeadLetterEnvelope(
    Guid MessageId,
    Guid EventId,
    string EventType,
    Guid CorrelationId,
    Guid? CausationId,
    string TraceId,
    EventPriority Priority,
    int Attempts,
    string FailureReason,
    IIntegrationEvent Event,
    DateTimeOffset DeadLetteredOnUtc);
