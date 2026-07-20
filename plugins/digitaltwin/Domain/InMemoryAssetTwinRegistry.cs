using System.Collections.Concurrent;

namespace FactoryOS.Plugins.DigitalTwin.Domain;

/// <summary>
/// The default in-memory <see cref="IAssetTwinRegistry"/>. State is partitioned by tenant — no code path crosses
/// tenants — and each tenant's twins are guarded by one lock. Every fold guards against out-of-order delivery by
/// keeping the newer observation, so a twin only ever advances in time. Redis-swappable, same contract.
/// </summary>
public sealed class InMemoryAssetTwinRegistry : IAssetTwinRegistry
{
    private sealed class AssetState
    {
        public Dictionary<string, MetricReading> Metrics { get; } = new(StringComparer.Ordinal);

        public AssetHealth? Health { get; set; }

        public DateTimeOffset LastUpdatedAt { get; set; }
    }

    private sealed class TenantTwins
    {
        public Lock Gate { get; } = new();

        public Dictionary<string, AssetState> Assets { get; } = new(StringComparer.Ordinal);
    }

    private readonly ConcurrentDictionary<string, TenantTwins> _tenants = new(StringComparer.Ordinal);
    private readonly decimal _degradedThreshold;

    /// <summary>Initializes a new instance of the <see cref="InMemoryAssetTwinRegistry"/> class.</summary>
    /// <param name="options">The module options carrying the degraded-status threshold.</param>
    public InMemoryAssetTwinRegistry(DigitalTwinOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _degradedThreshold = options.DegradedOeeThreshold;
    }

    /// <inheritdoc />
    public void RecordMetric(string tenant, string assetId, MetricReading reading)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var twins = _tenants.GetOrAdd(tenant, static _ => new TenantTwins());
        lock (twins.Gate)
        {
            var asset = GetOrAddAsset(twins, assetId);
            if (asset.Metrics.TryGetValue(reading.Metric, out var existing) && existing.At > reading.At)
            {
                return; // an equal-or-newer reading is already held
            }

            asset.Metrics[reading.Metric] = reading;
            Advance(asset, reading.At);
        }
    }

    /// <inheritdoc />
    public void RecordHealth(string tenant, string assetId, AssetHealth health)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var twins = _tenants.GetOrAdd(tenant, static _ => new TenantTwins());
        lock (twins.Gate)
        {
            var asset = GetOrAddAsset(twins, assetId);
            if (asset.Health is { } existing && existing.At > health.At)
            {
                return;
            }

            asset.Health = health;
            Advance(asset, health.At);
        }
    }

    /// <inheritdoc />
    public AssetTwin? Get(string tenant, string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        if (!_tenants.TryGetValue(tenant, out var twins))
        {
            return null;
        }

        lock (twins.Gate)
        {
            if (!twins.Assets.TryGetValue(assetId, out var asset))
            {
                return null;
            }

            var metrics = asset.Metrics.Values
                .OrderBy(static m => m.Metric, StringComparer.Ordinal)
                .ToArray();

            return new AssetTwin(assetId, metrics, asset.Health, asset.LastUpdatedAt, Status(asset));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Assets(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        if (!_tenants.TryGetValue(tenant, out var twins))
        {
            return [];
        }

        lock (twins.Gate)
        {
            return twins.Assets.Keys.OrderBy(static id => id, StringComparer.Ordinal).ToArray();
        }
    }

    private static AssetState GetOrAddAsset(TenantTwins twins, string assetId)
    {
        if (!twins.Assets.TryGetValue(assetId, out var asset))
        {
            asset = new AssetState();
            twins.Assets[assetId] = asset;
        }

        return asset;
    }

    private static void Advance(AssetState asset, DateTimeOffset at)
    {
        if (at > asset.LastUpdatedAt)
        {
            asset.LastUpdatedAt = at;
        }
    }

    private string Status(AssetState asset)
    {
        if (asset.Metrics.Count == 0 && asset.Health is null)
        {
            return TwinStatus.Unknown;
        }

        if (asset.Health is { MeetsTarget: false } health && health.Oee <= _degradedThreshold)
        {
            return TwinStatus.Degraded;
        }

        return TwinStatus.Online;
    }
}
