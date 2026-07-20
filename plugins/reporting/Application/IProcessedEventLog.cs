namespace FactoryOS.Plugins.Reporting.Application;

/// <summary>
/// Records which events the report has already folded in, so at-least-once delivery does not skew a daily
/// average. The realization of the "idempotent consumers deduplicate by event id" invariant.
/// </summary>
public interface IProcessedEventLog
{
    /// <summary>Atomically marks an event processed, reporting whether this was the first time.</summary>
    /// <param name="eventId">The event's stable identifier.</param>
    /// <returns><see langword="true"/> if newly marked; <see langword="false"/> if it was already processed.</returns>
    bool TryMarkProcessed(Guid eventId);
}
