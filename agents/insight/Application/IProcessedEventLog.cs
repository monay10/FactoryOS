namespace FactoryOS.Agents.Insight.Application;

/// <summary>
/// Records which triggers the agent has already produced an insight for, so at-least-once delivery does not
/// generate (and pay for) the same insight twice. The realization of "idempotent consumers deduplicate by event id".
/// </summary>
public interface IProcessedEventLog
{
    /// <summary>Atomically marks an event processed, reporting whether this was the first time.</summary>
    /// <param name="eventId">The event's stable identifier.</param>
    /// <returns><see langword="true"/> if newly marked; <see langword="false"/> if it was already processed.</returns>
    bool TryMarkProcessed(Guid eventId);
}
