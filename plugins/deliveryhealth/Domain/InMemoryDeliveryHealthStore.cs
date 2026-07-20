using System.Collections.Concurrent;

namespace FactoryOS.Plugins.DeliveryHealth.Domain;

/// <summary>
/// The default in-memory <see cref="IDeliveryHealthStore"/>. State is partitioned by tenant — no code path crosses
/// tenants — and each tenant's state is guarded by its own lock. Per-transport tallies accumulate without bound
/// (one entry per distinct transport); the recent-failure list is kept newest-first and bounded to a configured
/// capacity. Duplicate delivery events are dropped under the same lock so a redelivery never double-counts.
/// </summary>
public sealed class InMemoryDeliveryHealthStore : IDeliveryHealthStore
{
    private sealed class Counters
    {
        public int Attempts { get; set; }

        public int Delivered { get; set; }

        public int Failed { get; set; }

        public int ConsecutiveFailures { get; set; }
    }

    private sealed class TenantHealth
    {
        public Lock Gate { get; } = new();

        public Dictionary<string, Counters> Transports { get; } = new(StringComparer.Ordinal);

        public LinkedList<DeliveryFailure> Failures { get; } = new();

        public HashSet<Guid> Seen { get; } = [];
    }

    private readonly ConcurrentDictionary<string, TenantHealth> _tenants = new(StringComparer.Ordinal);
    private readonly int _failureCapacity;

    /// <summary>Initializes a new instance of the <see cref="InMemoryDeliveryHealthStore"/> class.</summary>
    /// <param name="options">The module options carrying the recent-failure capacity.</param>
    public InMemoryDeliveryHealthStore(DeliveryHealthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _failureCapacity = Math.Max(1, options.RecentFailureCapacity);
    }

    /// <inheritdoc />
    public RecordOutcome Record(string tenant, Guid sourceEventId, string transport, string channel, string subject, bool delivered, string? detail, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(transport);

        var health = _tenants.GetOrAdd(tenant, static _ => new TenantHealth());
        lock (health.Gate)
        {
            if (!health.Seen.Add(sourceEventId))
            {
                return default; // Recorded == false
            }

            if (!health.Transports.TryGetValue(transport, out var counters))
            {
                counters = new Counters();
                health.Transports[transport] = counters;
            }

            counters.Attempts++;
            if (delivered)
            {
                counters.Delivered++;
                counters.ConsecutiveFailures = 0;
            }
            else
            {
                counters.Failed++;
                counters.ConsecutiveFailures++;
                health.Failures.AddFirst(new DeliveryFailure(transport, channel, subject, detail, at));
                if (health.Failures.Count > _failureCapacity)
                {
                    health.Failures.RemoveLast();
                }
            }

            return new RecordOutcome(true, counters.Attempts, counters.Delivered, counters.Failed, counters.ConsecutiveFailures);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<TransportHealth> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        if (!_tenants.TryGetValue(tenant, out var health))
        {
            return [];
        }

        lock (health.Gate)
        {
            return health.Transports
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new TransportHealth(pair.Key, pair.Value.Attempts, pair.Value.Delivered, pair.Value.Failed))
                .ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DeliveryFailure> RecentFailures(string tenant, int max)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        if (max <= 0 || !_tenants.TryGetValue(tenant, out var health))
        {
            return [];
        }

        lock (health.Gate)
        {
            var take = Math.Min(max, health.Failures.Count);
            var result = new DeliveryFailure[take];
            var node = health.Failures.First;
            for (var i = 0; i < take && node is not null; i++, node = node.Next)
            {
                result[i] = node.Value;
            }

            return result;
        }
    }
}
