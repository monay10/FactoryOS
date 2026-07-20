namespace FactoryOS.Plugins.Scheduler.Domain;

/// <summary>
/// The pure decision at the heart of the Scheduler: given when a schedule last ran, the current instant and its
/// interval, is it due now? A schedule that has never run is due on its first pulse; thereafter it is due once
/// its interval has elapsed. No state, no I/O.
/// </summary>
public static class ScheduleEvaluator
{
    /// <summary>Decides whether a schedule is due.</summary>
    /// <param name="lastRun">When the schedule last fired, or <see langword="null"/> if it never has.</param>
    /// <param name="now">The current instant (from the pulse).</param>
    /// <param name="interval">The schedule's fixed cadence; zero or negative fires on every pulse.</param>
    /// <returns><see langword="true"/> if the schedule should fire now.</returns>
    public static bool IsDue(DateTimeOffset? lastRun, DateTimeOffset now, TimeSpan interval)
    {
        if (lastRun is null)
        {
            return true;
        }

        if (interval <= TimeSpan.Zero)
        {
            return true;
        }

        return now - lastRun.Value >= interval;
    }
}
