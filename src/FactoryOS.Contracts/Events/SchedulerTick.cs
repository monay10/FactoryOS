namespace FactoryOS.Contracts.Events;

/// <summary>
/// A clock pulse for a tenant, emitted by the host or edge timer (the clock lives outside the modules). The
/// Scheduler consumes it and decides which schedules are due; no module produces its own time. Carrying the
/// instant on the event keeps scheduling deterministic and testable — the decision depends only on the pulse.
/// </summary>
public sealed record SchedulerTick : IntegrationEvent
{
    /// <summary>The tenant the pulse is for.</summary>
    public required string Tenant { get; init; }

    /// <summary>The instant this pulse represents.</summary>
    public DateTimeOffset Instant { get; init; }
}
