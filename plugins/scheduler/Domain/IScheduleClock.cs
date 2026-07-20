namespace FactoryOS.Plugins.Scheduler.Domain;

/// <summary>
/// The tenant-scoped record of when each schedule last fired, and the atomic gate that decides — and claims — a
/// due firing. Claiming is compare-and-set: two concurrent pulses cannot both fire the same schedule for the
/// same interval, which also makes a redelivered pulse a no-op.
/// </summary>
public interface IScheduleClock
{
    /// <summary>
    /// Atomically decides whether a schedule is due at <paramref name="now"/> and, if so, records the firing.
    /// </summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="scheduleId">The schedule id.</param>
    /// <param name="now">The current instant (from the pulse).</param>
    /// <param name="interval">The schedule's fixed cadence.</param>
    /// <returns><see langword="true"/> if the schedule fired now; <see langword="false"/> if it was not due.</returns>
    bool TryClaim(string tenant, string scheduleId, DateTimeOffset now, TimeSpan interval);
}
