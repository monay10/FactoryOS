namespace FactoryOS.Agents.Brain.Application;

/// <summary>
/// Records which questions the agent has already answered, so at-least-once delivery does not answer twice. The
/// realization of the "idempotent consumers deduplicate by event id" invariant.
/// </summary>
public interface IProcessedEventLog
{
    /// <summary>Atomically marks a question answered, reporting whether this was the first time.</summary>
    /// <param name="eventId">The question event's stable identifier.</param>
    /// <returns><see langword="true"/> if newly marked; <see langword="false"/> if already answered.</returns>
    bool TryMarkProcessed(Guid eventId);
}
