using System.Collections.Concurrent;

namespace FactoryOS.Agents.Insight.Domain;

/// <summary>
/// The default <see cref="IInsightFeed"/>: a per-tenant bounded buffer of recent insights, held in memory. Each
/// tenant keeps at most <see cref="Capacity"/> insights; recording a fresh one past the cap evicts the oldest, so
/// the feed stays a rolling window. Deduplication is by event id per tenant. Tenants never share state.
/// </summary>
public sealed class InMemoryInsightFeed : IInsightFeed
{
    /// <summary>The most insights kept per tenant before the oldest is evicted.</summary>
    public const int Capacity = 200;

    private sealed class TenantFeed
    {
        public LinkedList<InsightRecord> Records { get; } = new();

        public HashSet<Guid> Seen { get; } = [];
    }

    private readonly ConcurrentDictionary<string, TenantFeed> _byTenant = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryRecord(string tenant, InsightRecord record)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var feed = _byTenant.GetOrAdd(tenant, static _ => new TenantFeed());
        lock (feed)
        {
            if (!feed.Seen.Add(record.EventId))
            {
                return false;
            }

            feed.Records.AddFirst(record); // newest first
            if (feed.Records.Count > Capacity)
            {
                var oldest = feed.Records.Last!.Value;
                feed.Records.RemoveLast();
                feed.Seen.Remove(oldest.EventId);
            }

            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<InsightRecord> Recent(string tenant, int max)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var take = Math.Max(1, max);
        if (!_byTenant.TryGetValue(tenant, out var feed))
        {
            return [];
        }

        lock (feed)
        {
            return feed.Records.Take(take).ToList();
        }
    }

    /// <inheritdoc />
    public InsightFeedSummary Summarize(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        if (!_byTenant.TryGetValue(tenant, out var feed))
        {
            return new InsightFeedSummary(tenant, 0, []);
        }

        lock (feed)
        {
            var byTrigger = feed.Records
                .GroupBy(static r => r.TriggerType, StringComparer.Ordinal)
                .Select(static g => new InsightTriggerTally(g.Key, g.Count()))
                .OrderByDescending(static t => t.Count)
                .ThenBy(static t => t.TriggerType, StringComparer.Ordinal)
                .ToList();

            return new InsightFeedSummary(tenant, feed.Records.Count, byTrigger);
        }
    }
}
