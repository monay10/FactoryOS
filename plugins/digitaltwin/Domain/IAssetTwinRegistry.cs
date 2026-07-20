namespace FactoryOS.Plugins.DigitalTwin.Domain;

/// <summary>
/// The tenant-scoped registry of asset twins: the write side (fed by telemetry and OEE handlers) folds in the
/// latest metric and health; the read side asks for an asset's current twin. A CQRS read model kept current
/// purely by consuming the event bus. Out-of-order observations are ignored — a twin only moves forward in time.
/// </summary>
public interface IAssetTwinRegistry
{
    /// <summary>Folds a metric reading into an asset's twin, unless an equal-or-newer reading is already held.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="assetId">The asset.</param>
    /// <param name="reading">The metric reading.</param>
    void RecordMetric(string tenant, string assetId, MetricReading reading);

    /// <summary>Folds an OEE health reading into an asset's twin, unless an equal-or-newer one is already held.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="assetId">The asset.</param>
    /// <param name="health">The health reading.</param>
    void RecordHealth(string tenant, string assetId, AssetHealth health);

    /// <summary>Returns an asset's current twin, or <see langword="null"/> if nothing has been observed for it.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="assetId">The asset.</param>
    /// <returns>The twin snapshot, or <see langword="null"/>.</returns>
    AssetTwin? Get(string tenant, string assetId);

    /// <summary>Returns the ids of all assets a tenant has a twin for, ordered.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The asset ids.</returns>
    IReadOnlyList<string> Assets(string tenant);
}
