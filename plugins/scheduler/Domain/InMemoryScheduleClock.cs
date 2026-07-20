using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Scheduler.Domain;

/// <summary>
/// The default in-memory <see cref="IScheduleClock"/>. Last-run instants are partitioned by tenant — no code
/// path crosses tenants — and each tenant's state is guarded by its own lock, so the due-decision and the
/// last-run update happen as one atomic claim.
/// </summary>
public sealed class InMemoryScheduleClock : IScheduleClock
{
    private sealed class TenantClock
    {
        public Lock Gate { get; } = new();

        public Dictionary<string, DateTimeOffset> LastRun { get; } = new(StringComparer.Ordinal);
    }

    private readonly ConcurrentDictionary<string, TenantClock> _clocks = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryClaim(string tenant, string scheduleId, DateTimeOffset now, TimeSpan interval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduleId);

        var clock = _clocks.GetOrAdd(tenant, static _ => new TenantClock());
        lock (clock.Gate)
        {
            var lastRun = clock.LastRun.TryGetValue(scheduleId, out var previous)
                ? previous
                : (DateTimeOffset?)null;

            if (!ScheduleEvaluator.IsDue(lastRun, now, interval))
            {
                return false;
            }

            clock.LastRun[scheduleId] = now;
            return true;
        }
    }
}
