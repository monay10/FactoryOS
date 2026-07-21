using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Monitoring.Configuration;
using FactoryOS.Plugins.Workflow.Monitoring.Diagnostics;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Events;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>
/// The heart of the monitoring engine: it takes measurements in, judges them against the thresholds that watch
/// them, opens and closes alerts, runs health probes, and applies retention.
/// <para>
/// Every one of those is a <b>read</b> of what the platform did. The runtime consumes the events the engines
/// above it publish and writes nothing back to any of them — no engine holds a reference to monitoring, and
/// none was modified to be monitored.
/// </para>
/// </summary>
public sealed class MonitoringRuntime
{
    private readonly MetricCollector _collector;
    private readonly MetricAggregator _aggregator;
    private readonly MetricRetentionManager _retention;
    private readonly ThresholdEvaluator _thresholds;
    private readonly AlertEvaluator _alerts;
    private readonly MetricSearchService _search;
    private readonly HealthEngine _health;
    private readonly MonitoringDispatcher _dispatcher;
    private readonly MonitoringMetrics _metrics;
    private readonly MonitoringEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="MonitoringRuntime"/> class.</summary>
    /// <param name="collector">The collector.</param>
    /// <param name="aggregator">The aggregator.</param>
    /// <param name="retention">The retention manager.</param>
    /// <param name="thresholds">The threshold evaluator.</param>
    /// <param name="alerts">The alert evaluator.</param>
    /// <param name="search">The search service.</param>
    /// <param name="health">The health runtime.</param>
    /// <param name="dispatcher">The event dispatcher.</param>
    /// <param name="metrics">The engine's own counters.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public MonitoringRuntime(
        MetricCollector collector,
        MetricAggregator aggregator,
        MetricRetentionManager retention,
        ThresholdEvaluator thresholds,
        AlertEvaluator alerts,
        MetricSearchService search,
        HealthEngine health,
        MonitoringDispatcher dispatcher,
        MonitoringMetrics metrics,
        MonitoringEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(collector);
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentNullException.ThrowIfNull(thresholds);
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _collector = collector;
        _aggregator = aggregator;
        _retention = retention;
        _thresholds = thresholds;
        _alerts = alerts;
        _search = search;
        _health = health;
        _dispatcher = dispatcher;
        _metrics = metrics;
        _options = options;
        _clock = clock;
    }

    /// <summary>Records a measurement.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="metricKey">The metric.</param>
    /// <param name="value">The measured number.</param>
    /// <param name="dimension">The labels slicing the series.</param>
    /// <param name="correlation">The operation it came from.</param>
    /// <param name="timestampUtc">When it was measured.</param>
    /// <returns>What was done with the measurement.</returns>
    public MetricCollection Record(
        string tenant,
        string metricKey,
        double value,
        MetricDimension? dimension = null,
        MetricCorrelation? correlation = null,
        DateTimeOffset? timestampUtc = null)
    {
        var collection = _collector.Record(tenant, metricKey, value, dimension, correlation, timestampUtc);
        if (!collection.WasRecorded)
        {
            _metrics.RecordSampled();
            return collection;
        }

        _metrics.RecordCollected();
        if (_options.PublishCollectionEvents)
        {
            _dispatcher.Publish(new MetricCollected(
                tenant, collection.Value!.TimestampUtc, collection.Instance, collection.Value));
        }

        return collection;
    }

    /// <summary>Records a single occurrence of a counter metric.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="metricKey">The metric.</param>
    /// <param name="dimension">The labels slicing the series.</param>
    /// <param name="correlation">The operation it came from.</param>
    /// <param name="timestampUtc">When it happened.</param>
    /// <returns>What was done with the measurement.</returns>
    public MetricCollection Count(
        string tenant,
        string metricKey,
        MetricDimension? dimension = null,
        MetricCorrelation? correlation = null,
        DateTimeOffset? timestampUtc = null) =>
        Record(tenant, metricKey, 1, dimension, correlation, timestampUtc);

    /// <summary>Collapses the series a query selects and announces each snapshot.</summary>
    /// <param name="query">The filters.</param>
    /// <returns>The snapshots.</returns>
    public IReadOnlyList<MetricSnapshot> Aggregate(MetricQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _clock.UtcNow;
        var snapshots = _search.Search(query, now);
        _metrics.RecordAggregations(snapshots.Count);

        foreach (var snapshot in snapshots)
        {
            _dispatcher.Publish(new MetricAggregated(query.Tenant, now, snapshot));
        }

        return snapshots;
    }

    /// <summary>
    /// Judges every threshold against a tenant's series, then opens and closes the alerts that follow. This is
    /// the pass a scheduler runs: evaluation is deliberately pull-based rather than firing on every
    /// measurement, because a threshold judges a window and a window is only meaningful when something asks
    /// about it.
    /// </summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>Every verdict reached this pass.</returns>
    public IReadOnlyList<ThresholdEvaluation> Evaluate(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var now = _clock.UtcNow;
        var evaluations = _thresholds.EvaluateAll(tenant, now);
        var exceeded = evaluations.Where(evaluation => evaluation.IsExceeded).ToArray();
        _metrics.RecordThresholdBreaches(exceeded.Length);

        foreach (var evaluation in exceeded)
        {
            _dispatcher.Publish(new ThresholdExceeded(
                tenant,
                now,
                evaluation.Threshold.Key,
                evaluation.Snapshot.Instance,
                evaluation.State,
                evaluation.Snapshot.Value,
                evaluation.Limit,
                evaluation.Snapshot.Correlation));
        }

        var changes = _alerts.Evaluate(tenant, evaluations, now);
        if (changes.IsEmpty)
        {
            return evaluations;
        }

        _metrics.RecordAlertsTriggered(changes.Triggered.Count);
        _metrics.RecordAlertsResolved(changes.Resolved.Count);

        foreach (var alert in changes.Triggered)
        {
            _dispatcher.Publish(new AlertTriggered(tenant, now, alert));
        }

        foreach (var (alert, openFor) in changes.Resolved)
        {
            _dispatcher.Publish(new AlertResolved(
                tenant, now, alert.Key, alert.RuleKey, alert.Instance, openFor));
        }

        return evaluations;
    }

    /// <summary>Applies retention to a tenant's series.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>What the pass did.</returns>
    public MetricRetentionSummary RunRetention(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var now = _clock.UtcNow;
        var summary = _retention.Run(tenant, now);
        if (!summary.ChangedAnything)
        {
            return summary;
        }

        _metrics.RecordExpired(summary.Removed);
        _dispatcher.Publish(new MetricRetentionExpired(tenant, now, summary.Removed, summary.RolledUp));
        return summary;
    }

    /// <summary>Runs one health probe.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="checkKey">The check.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the probe found.</returns>
    public async Task<HealthCheckResult> CheckAsync(
        string tenant, string checkKey, CancellationToken cancellationToken = default)
    {
        var result = await _health.CheckAsync(tenant, checkKey, cancellationToken).ConfigureAwait(false);
        _metrics.RecordHealthCheck();
        return result;
    }

    /// <summary>Runs every health probe and reports what they add up to.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The report.</returns>
    public async Task<HealthReport> CheckHealthAsync(
        string tenant, CancellationToken cancellationToken = default)
    {
        var report = await _health.CheckAllAsync(tenant, cancellationToken).ConfigureAwait(false);
        _metrics.RecordHealthChecks(report.Results.Count);
        return report;
    }

    /// <summary>Reports the last known health without running any probe.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The report.</returns>
    public HealthReport LastHealthReport(string tenant) => _health.LastReport(tenant);

    /// <summary>Gets the recorded history of a health check.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="checkKey">The check.</param>
    /// <returns>The history, newest last.</returns>
    public IReadOnlyList<HealthCheckResult> HealthHistory(string tenant, string checkKey) =>
        _health.History(tenant, checkKey);

    /// <summary>Reads a snapshot of one series over a window.</summary>
    /// <param name="instance">The series.</param>
    /// <param name="aggregation">How to collapse it.</param>
    /// <param name="window">The window, ending now.</param>
    /// <returns>The snapshot.</returns>
    public MetricSnapshot Snapshot(
        MetricInstance instance, MetricAggregation aggregation, TimeSpan? window = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var now = _clock.UtcNow;
        var from = now - (window ?? _options.DefaultWindow);
        var series = _search.Series(new MetricQuery(instance.Tenant) { MetricKey = instance.MetricKey })
            .FirstOrDefault(candidate => candidate.Instance == instance);

        return series is null
            ? MetricSnapshot.Empty(instance, aggregation, from, now)
            : _aggregator.Aggregate(series, aggregation, from, now);
    }

    /// <summary>Runs a search over the metric store.</summary>
    /// <param name="query">The filters.</param>
    /// <returns>The snapshots.</returns>
    public IReadOnlyList<MetricSnapshot> Search(MetricQuery query) => _search.Search(query, _clock.UtcNow);

    /// <summary>Finds every measurement one operation produced.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>The measurements.</returns>
    public IReadOnlyList<(MetricInstance Instance, MetricValue Value)> ByCorrelation(
        string tenant, string correlationId) => _search.ByCorrelation(tenant, correlationId);

    /// <summary>Finds every measurement belonging to one distributed trace.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="traceId">The trace id.</param>
    /// <returns>The measurements.</returns>
    public IReadOnlyList<(MetricInstance Instance, MetricValue Value)> ByTrace(string tenant, string traceId) =>
        _search.ByTrace(tenant, traceId);

    /// <summary>Gets the alerts currently open for a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The open alerts.</returns>
    public IReadOnlyList<MetricAlert> OpenAlerts(string tenant) => _alerts.OpenAlerts(tenant);
}
