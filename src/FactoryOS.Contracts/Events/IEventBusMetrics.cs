namespace FactoryOS.Contracts.Events;

/// <summary>Records counters describing event-bus activity for monitoring and diagnostics.</summary>
public interface IEventBusMetrics
{
    /// <summary>Records that an event was published.</summary>
    /// <param name="eventType">The CLR type name of the event.</param>
    /// <param name="priority">The priority the event was published with.</param>
    void RecordPublished(string eventType, EventPriority priority);

    /// <summary>Records that a handler successfully handled an event.</summary>
    /// <param name="eventType">The CLR type name of the event.</param>
    void RecordHandled(string eventType);

    /// <summary>Records that a handler failed and a retry was scheduled.</summary>
    /// <param name="eventType">The CLR type name of the event.</param>
    void RecordRetry(string eventType);

    /// <summary>Records that a message was dead-lettered after exhausting its retries.</summary>
    /// <param name="eventType">The CLR type name of the event.</param>
    void RecordDeadLettered(string eventType);
}
