namespace FactoryOS.Plugins.DigitalTwin.Domain;

/// <summary>
/// An immutable snapshot of one asset's live digital twin: its latest metric gauges, its latest OEE health (if
/// any), when it last reported, and a derived status. A pure reflection of the events that shaped the asset.
/// </summary>
/// <param name="AssetId">The asset the twin mirrors.</param>
/// <param name="Metrics">Latest value per metric, ordered by metric name.</param>
/// <param name="Health">The latest OEE health, or <see langword="null"/> if none observed.</param>
/// <param name="LastUpdatedAt">The most recent observation time across all inputs.</param>
/// <param name="Status">The derived status, one of <see cref="TwinStatus"/>.</param>
public sealed record AssetTwin(
    string AssetId,
    IReadOnlyList<MetricReading> Metrics,
    AssetHealth? Health,
    DateTimeOffset LastUpdatedAt,
    string Status);
