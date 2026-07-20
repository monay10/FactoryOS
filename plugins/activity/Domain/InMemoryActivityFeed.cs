using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Activity.Domain;

/// <summary>
/// The default in-memory <see cref="IActivityFeed"/>. Entries are partitioned by tenant — no code path crosses
/// tenants — and each tenant's feed is guarded by its own lock, kept newest-first and bounded to a configured
/// capacity so the feed cannot grow without limit. Duplicate source events are dropped under the same lock.
/// </summary>
public sealed class InMemoryActivityFeed : IActivityFeed
{
    private sealed class TenantFeed
    {
        public Lock Gate { get; } = new();

        public LinkedList<ActivityEntry> Entries { get; } = new();

        public HashSet<Guid> Seen { get; } = [];
    }

    private readonly ConcurrentDictionary<string, TenantFeed> _feeds = new(StringComparer.Ordinal);
    private readonly int _capacity;

    /// <summary>Initializes a new instance of the <see cref="InMemoryActivityFeed"/> class.</summary>
    /// <param name="options">The module options carrying the feed capacity.</param>
    public InMemoryActivityFeed(ActivityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _capacity = Math.Max(1, options.FeedCapacity);
    }

    /// <inheritdoc />
    public bool Record(ActivityEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Tenant);

        var feed = _feeds.GetOrAdd(entry.Tenant, static _ => new TenantFeed());
        lock (feed.Gate)
        {
            if (!feed.Seen.Add(entry.SourceEventId))
            {
                return false;
            }

            feed.Entries.AddFirst(entry);

            if (feed.Entries.Count > _capacity)
            {
                var evicted = feed.Entries.Last!.Value;
                feed.Entries.RemoveLast();
                feed.Seen.Remove(evicted.SourceEventId);
            }

            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ActivityEntry> Recent(string tenant, int max, string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        if (max <= 0 || !_feeds.TryGetValue(tenant, out var feed))
        {
            return [];
        }

        var filter = string.IsNullOrWhiteSpace(category) ? null : category;

        lock (feed.Gate)
        {
            var result = new List<ActivityEntry>(Math.Min(max, feed.Entries.Count));
            for (var node = feed.Entries.First; node is not null && result.Count < max; node = node.Next)
            {
                if (filter is null || string.Equals(node.Value.Category, filter, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(node.Value);
                }
            }

            return result;
        }
    }

    /// <inheritdoc />
    public ActivityFeedSummary Summarize(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        if (!_feeds.TryGetValue(tenant, out var feed))
        {
            return new ActivityFeedSummary(tenant, 0, []);
        }

        int total;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        lock (feed.Gate)
        {
            total = feed.Entries.Count;
            for (var node = feed.Entries.First; node is not null; node = node.Next)
            {
                var category = node.Value.Category;
                counts[category] = counts.TryGetValue(category, out var current) ? current + 1 : 1;
            }
        }

        var byCategory = counts
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new ActivityCategoryTally(pair.Key, pair.Value))
            .ToList();

        return new ActivityFeedSummary(tenant, total, byCategory);
    }
}
