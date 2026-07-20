using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Reporting.Domain;

/// <summary>
/// The default in-memory <see cref="IOeeReport"/>. State is partitioned by tenant — no code path crosses
/// tenants — and each tenant's report is guarded by its own lock. Each machine keeps its day buckets in
/// ascending order, so the oldest is trimmed first once the retention cap is reached. A production deployment
/// would back this with a read-model store; the contract is identical.
/// </summary>
public sealed class InMemoryOeeReport : IOeeReport
{
    private sealed class Accumulator
    {
        public int Count { get; set; }

        public decimal Sum { get; set; }

        public decimal Min { get; set; }

        public decimal Max { get; set; }

        public OeeDailyStat ToStat(DateOnly day) =>
            new(day, Count, Count > 0 ? Sum / Count : 0m, Min, Max);
    }

    private sealed class TenantReport
    {
        public Lock Gate { get; } = new();

        public Dictionary<string, SortedDictionary<DateOnly, Accumulator>> Machines { get; } =
            new(StringComparer.Ordinal);
    }

    private readonly ConcurrentDictionary<string, TenantReport> _reports = new(StringComparer.Ordinal);
    private readonly int _retainDays;

    /// <summary>Initializes a new instance of the <see cref="InMemoryOeeReport"/> class.</summary>
    /// <param name="options">The module options carrying the retention cap.</param>
    public InMemoryOeeReport(ReportingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _retainDays = Math.Max(1, options.RetainDays);
    }

    /// <inheritdoc />
    public void Record(string tenant, string machineId, DateOnly day, decimal oee)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        var report = _reports.GetOrAdd(tenant, static _ => new TenantReport());
        lock (report.Gate)
        {
            if (!report.Machines.TryGetValue(machineId, out var days))
            {
                days = new SortedDictionary<DateOnly, Accumulator>();
                report.Machines[machineId] = days;
            }

            if (!days.TryGetValue(day, out var accumulator))
            {
                accumulator = new Accumulator { Min = oee, Max = oee };
                days[day] = accumulator;
            }

            accumulator.Count++;
            accumulator.Sum += oee;
            accumulator.Min = Math.Min(accumulator.Min, oee);
            accumulator.Max = Math.Max(accumulator.Max, oee);

            while (days.Count > _retainDays)
            {
                // SortedDictionary keys are ascending, so the first key is the oldest day.
                using var enumerator = days.Keys.GetEnumerator();
                enumerator.MoveNext();
                days.Remove(enumerator.Current);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<OeeDailyStat> ForMachine(string tenant, string machineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        if (!_reports.TryGetValue(tenant, out var report))
        {
            return [];
        }

        lock (report.Gate)
        {
            if (!report.Machines.TryGetValue(machineId, out var days) || days.Count == 0)
            {
                return [];
            }

            var stats = new OeeDailyStat[days.Count];
            var index = stats.Length - 1;
            foreach (var (day, accumulator) in days)
            {
                stats[index--] = accumulator.ToStat(day); // ascending source → newest first
            }

            return stats;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Machines(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        if (!_reports.TryGetValue(tenant, out var report))
        {
            return [];
        }

        lock (report.Gate)
        {
            return report.Machines.Keys.OrderBy(static id => id, StringComparer.Ordinal).ToArray();
        }
    }
}
