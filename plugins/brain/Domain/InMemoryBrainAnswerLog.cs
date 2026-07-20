using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Brain.Domain;

/// <summary>
/// The default in-memory <see cref="IBrainAnswerLog"/>. Entries are partitioned by tenant — no code path crosses
/// tenants — and each tenant's log is guarded by its own lock, kept newest-first and bounded to a configured
/// capacity so the log cannot grow without limit. Duplicate source events are dropped under the same lock.
/// </summary>
public sealed class InMemoryBrainAnswerLog : IBrainAnswerLog
{
    private sealed class TenantLog
    {
        public Lock Gate { get; } = new();

        public LinkedList<BrainAnswerEntry> Entries { get; } = new();

        public HashSet<Guid> Seen { get; } = [];
    }

    private readonly ConcurrentDictionary<string, TenantLog> _logs = new(StringComparer.Ordinal);
    private readonly int _capacity;

    /// <summary>Initializes a new instance of the <see cref="InMemoryBrainAnswerLog"/> class.</summary>
    /// <param name="options">The module options carrying the log capacity.</param>
    public InMemoryBrainAnswerLog(BrainReadModelOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _capacity = Math.Max(1, options.LogCapacity);
    }

    /// <inheritdoc />
    public bool Record(BrainAnswerEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Tenant);

        var log = _logs.GetOrAdd(entry.Tenant, static _ => new TenantLog());
        lock (log.Gate)
        {
            if (!log.Seen.Add(entry.SourceEventId))
            {
                return false;
            }

            log.Entries.AddFirst(entry);

            if (log.Entries.Count > _capacity)
            {
                var evicted = log.Entries.Last!.Value;
                log.Entries.RemoveLast();
                log.Seen.Remove(evicted.SourceEventId);
            }

            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<BrainAnswerEntry> Recent(string tenant, int max)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        if (max <= 0 || !_logs.TryGetValue(tenant, out var log))
        {
            return [];
        }

        lock (log.Gate)
        {
            var result = new List<BrainAnswerEntry>(Math.Min(max, log.Entries.Count));
            for (var node = log.Entries.First; node is not null && result.Count < max; node = node.Next)
            {
                result.Add(node.Value);
            }

            return result;
        }
    }

    /// <inheritdoc />
    public BrainAnswerLogSummary Summarize(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        if (!_logs.TryGetValue(tenant, out var log))
        {
            return new BrainAnswerLogSummary(tenant, 0, []);
        }

        int total;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        lock (log.Gate)
        {
            total = log.Entries.Count;
            for (var node = log.Entries.First; node is not null; node = node.Next)
            {
                var model = node.Value.Model;
                counts[model] = counts.TryGetValue(model, out var current) ? current + 1 : 1;
            }
        }

        var byModel = counts
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new BrainModelTally(pair.Key, pair.Value))
            .ToList();

        return new BrainAnswerLogSummary(tenant, total, byModel);
    }
}
