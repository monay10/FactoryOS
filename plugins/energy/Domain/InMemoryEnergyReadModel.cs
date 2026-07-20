using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Energy.Domain;

/// <summary>
/// The default in-memory <see cref="IEnergyReadModel"/>. State is partitioned by tenant — no code path crosses
/// tenants — and each tenant's state is guarded by its own lock. The latest reading is kept per meter+metric;
/// spikes are held newest-first and bounded to a configured capacity so the feed cannot grow without limit.
/// </summary>
public sealed class InMemoryEnergyReadModel : IEnergyReadModel
{
    private sealed class TenantState
    {
        public Lock Gate { get; } = new();

        public Dictionary<(string Meter, string Metric), EnergyMeterReading> Meters { get; } = new();

        public LinkedList<EnergySpikeEntry> Spikes { get; } = new();
    }

    private readonly ConcurrentDictionary<string, TenantState> _tenants = new(StringComparer.Ordinal);
    private readonly int _spikeCapacity;

    /// <summary>Initializes a new instance of the <see cref="InMemoryEnergyReadModel"/> class.</summary>
    /// <param name="options">The module options carrying the spike-feed capacity.</param>
    public InMemoryEnergyReadModel(EnergyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _spikeCapacity = Math.Max(1, options.SpikeFeedCapacity);
    }

    /// <inheritdoc />
    public void RecordReading(EnergyMeterReading reading)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reading.Tenant);

        var state = _tenants.GetOrAdd(reading.Tenant, static _ => new TenantState());
        lock (state.Gate)
        {
            state.Meters[(reading.MeterId, reading.Metric)] = reading;
        }
    }

    /// <inheritdoc />
    public void RecordSpike(EnergySpikeEntry spike)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spike.Tenant);

        var state = _tenants.GetOrAdd(spike.Tenant, static _ => new TenantState());
        lock (state.Gate)
        {
            state.Spikes.AddFirst(spike);
            if (state.Spikes.Count > _spikeCapacity)
            {
                state.Spikes.RemoveLast();
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<EnergyMeterReading> Meters(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        if (!_tenants.TryGetValue(tenant, out var state))
        {
            return [];
        }

        lock (state.Gate)
        {
            return state.Meters.Values
                .OrderBy(static m => m.MeterId, StringComparer.Ordinal)
                .ThenBy(static m => m.Metric, StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<EnergySpikeEntry> Spikes(string tenant, int max)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        if (max <= 0 || !_tenants.TryGetValue(tenant, out var state))
        {
            return [];
        }

        lock (state.Gate)
        {
            var result = new List<EnergySpikeEntry>(Math.Min(max, state.Spikes.Count));
            for (var node = state.Spikes.First; node is not null && result.Count < max; node = node.Next)
            {
                result.Add(node.Value);
            }

            return result;
        }
    }

    /// <inheritdoc />
    public EnergyReadModelSummary Summarize(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        if (!_tenants.TryGetValue(tenant, out var state))
        {
            return new EnergyReadModelSummary(tenant, 0, 0);
        }

        lock (state.Gate)
        {
            return new EnergyReadModelSummary(tenant, state.Meters.Count, state.Spikes.Count);
        }
    }
}
