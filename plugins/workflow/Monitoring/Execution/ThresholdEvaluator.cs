using FactoryOS.Plugins.Workflow.Monitoring.Configuration;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>The verdict a threshold reached about one series.</summary>
/// <param name="Threshold">The threshold that judged.</param>
/// <param name="Snapshot">What it judged.</param>
/// <param name="State">The verdict.</param>
/// <param name="Limit">The limit the verdict was measured against.</param>
public sealed record ThresholdEvaluation(
    MetricThreshold Threshold, MetricSnapshot Snapshot, MetricHealthState State, double Limit)
{
    /// <summary>Gets a value indicating whether the metric is outside its limits.</summary>
    public bool IsExceeded => State is MetricHealthState.Warning or MetricHealthState.Critical;
}

/// <summary>
/// Judges each series against the thresholds that watch its metric.
/// <para>
/// A threshold judges every series of its metric <i>separately</i> unless it names a dimension. That matters:
/// one collapsed email channel must be able to trip a delivery-failure threshold even while the other channels
/// are fine, which averaging across dimensions would hide.
/// </para>
/// </summary>
public sealed class ThresholdEvaluator
{
    private readonly IMetricRepository _definitions;
    private readonly IMetricStore _store;
    private readonly MetricAggregator _aggregator;
    private readonly MonitoringEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ThresholdEvaluator"/> class.</summary>
    /// <param name="definitions">The definition and threshold registry.</param>
    /// <param name="store">The series store.</param>
    /// <param name="aggregator">The aggregator.</param>
    /// <param name="options">The engine options carrying the default window.</param>
    public ThresholdEvaluator(
        IMetricRepository definitions,
        IMetricStore store,
        MetricAggregator aggregator,
        MonitoringEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(options);
        _definitions = definitions;
        _store = store;
        _aggregator = aggregator;
        _options = options;
    }

    /// <summary>Judges every series a threshold covers.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="threshold">The threshold.</param>
    /// <param name="nowUtc">The end of the window judged.</param>
    /// <returns>One verdict per covered series.</returns>
    public IReadOnlyList<ThresholdEvaluation> Evaluate(
        string tenant, MetricThreshold threshold, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(threshold);

        var definition = _definitions.Find(threshold.MetricKey);
        if (definition is null)
        {
            return [];
        }

        var aggregation = threshold.Aggregation ?? definition.DefaultAggregation;
        var window = threshold.Window ?? _options.DefaultWindow;
        var from = nowUtc - window;

        return _store.ListByMetric(tenant, threshold.MetricKey)
            .Where(series => threshold.Dimension is null || series.Instance.Dimension.Covers(threshold.Dimension))
            .Select(series =>
            {
                var snapshot = _aggregator.Aggregate(series, aggregation, from, nowUtc);
                var state = threshold.Evaluate(snapshot);
                return new ThresholdEvaluation(threshold, snapshot, state, LimitFor(threshold, state));
            })
            .ToArray();
    }

    /// <summary>Judges every registered threshold against a tenant's series.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="nowUtc">The end of the window judged.</param>
    /// <returns>Every verdict reached.</returns>
    public IReadOnlyList<ThresholdEvaluation> EvaluateAll(string tenant, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _definitions.Thresholds()
            .SelectMany(threshold => Evaluate(tenant, threshold, nowUtc))
            .ToArray();
    }

    private static double LimitFor(MetricThreshold threshold, MetricHealthState state) =>
        state == MetricHealthState.Warning && threshold.WarningAt is { } warning ? warning : threshold.CriticalAt;
}
