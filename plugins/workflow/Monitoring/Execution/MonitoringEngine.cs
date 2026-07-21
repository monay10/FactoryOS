using FactoryOS.Plugins.Workflow.Monitoring.Diagnostics;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>
/// The public entry point to the monitoring engine. It records measurements, aggregates and searches them,
/// evaluates thresholds and alerts, runs health probes and applies retention.
/// <para>
/// Monitoring sits at the outermost layer of the platform, below everything it observes. It consumes the events
/// each engine publishes and writes nothing back: no engine references this namespace, no engine was modified
/// to be measured, and deleting the monitoring folder would leave every one of them working exactly as before.
/// </para>
/// </summary>
public sealed class MonitoringEngine
{
    private readonly MonitoringRuntime _runtime;
    private readonly IMetricRepository _repository;
    private readonly HealthRegistry _health;
    private readonly MonitoringPermissionEvaluator _permissions;
    private readonly MonitoringMetrics _metrics;

    /// <summary>Initializes a new instance of the <see cref="MonitoringEngine"/> class.</summary>
    /// <param name="runtime">The monitoring runtime.</param>
    /// <param name="repository">The definition, threshold, rule and policy registry.</param>
    /// <param name="health">The health check registry.</param>
    /// <param name="permissions">The permission evaluator.</param>
    /// <param name="metrics">The engine's own counters.</param>
    public MonitoringEngine(
        MonitoringRuntime runtime,
        IMetricRepository repository,
        HealthRegistry health,
        MonitoringPermissionEvaluator permissions,
        MonitoringMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(metrics);
        _runtime = runtime;
        _repository = repository;
        _health = health;
        _permissions = permissions;
        _metrics = metrics;
    }

    /// <summary>Registers a metric definition.</summary>
    /// <param name="definition">The definition.</param>
    public void Register(MetricDefinition definition) => _repository.Register(definition);

    /// <summary>Registers a threshold.</summary>
    /// <param name="threshold">The threshold.</param>
    public void RegisterThreshold(MetricThreshold threshold) => _repository.RegisterThreshold(threshold);

    /// <summary>Registers an alert rule.</summary>
    /// <param name="rule">The rule.</param>
    public void RegisterAlertRule(MetricAlertRule rule) => _repository.RegisterAlertRule(rule);

    /// <summary>Registers a retention policy.</summary>
    /// <param name="policy">The policy.</param>
    public void RegisterRetention(MetricRetentionPolicy policy) => _repository.RegisterRetention(policy);

    /// <summary>Registers a health check and the probe that answers for it.</summary>
    /// <param name="check">The check.</param>
    /// <param name="probe">The probe.</param>
    public void RegisterHealthCheck(HealthCheck check, HealthProbe probe) => _health.Register(check, probe);

    /// <summary>Gets the registered metric definitions.</summary>
    /// <returns>The definitions.</returns>
    public IReadOnlyList<MetricDefinition> Definitions() => _repository.Definitions();

    /// <summary>Gets the registered health checks.</summary>
    /// <returns>The checks.</returns>
    public IReadOnlyList<HealthCheck> HealthChecks() => _health.Checks();

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
        DateTimeOffset? timestampUtc = null) =>
        _runtime.Record(tenant, metricKey, value, dimension, correlation, timestampUtc);

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
        _runtime.Count(tenant, metricKey, dimension, correlation, timestampUtc);

    /// <summary>Collapses the series a query selects, announcing each snapshot.</summary>
    /// <param name="query">The filters.</param>
    /// <returns>The snapshots.</returns>
    public IReadOnlyList<MetricSnapshot> Aggregate(MetricQuery query) => _runtime.Aggregate(query);

    /// <summary>Reads a snapshot of one series over a window, announcing nothing.</summary>
    /// <param name="instance">The series.</param>
    /// <param name="aggregation">How to collapse it.</param>
    /// <param name="window">The window, ending now.</param>
    /// <returns>The snapshot.</returns>
    public MetricSnapshot Snapshot(
        MetricInstance instance, MetricAggregation aggregation, TimeSpan? window = null) =>
        _runtime.Snapshot(instance, aggregation, window);

    /// <summary>Runs a search over the metric store.</summary>
    /// <param name="query">The filters.</param>
    /// <returns>The snapshots.</returns>
    public IReadOnlyList<MetricSnapshot> Search(MetricQuery query) => _runtime.Search(query);

    /// <summary>Finds every measurement one operation produced.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>The measurements.</returns>
    public IReadOnlyList<(MetricInstance Instance, MetricValue Value)> ByCorrelation(
        string tenant, string correlationId) => _runtime.ByCorrelation(tenant, correlationId);

    /// <summary>Finds every measurement belonging to one distributed trace.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="traceId">The trace id.</param>
    /// <returns>The measurements.</returns>
    public IReadOnlyList<(MetricInstance Instance, MetricValue Value)> ByTrace(string tenant, string traceId) =>
        _runtime.ByTrace(tenant, traceId);

    /// <summary>Judges every threshold against a tenant's series and opens or closes the alerts that follow.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>Every verdict reached.</returns>
    public IReadOnlyList<ThresholdEvaluation> Evaluate(string tenant) => _runtime.Evaluate(tenant);

    /// <summary>Gets the alerts currently open for a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The open alerts.</returns>
    public IReadOnlyList<MetricAlert> OpenAlerts(string tenant) => _runtime.OpenAlerts(tenant);

    /// <summary>Applies retention to a tenant's series.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>What the pass did.</returns>
    public MetricRetentionSummary RunRetention(string tenant) => _runtime.RunRetention(tenant);

    /// <summary>Runs one health probe.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="checkKey">The check.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the probe found.</returns>
    public Task<HealthCheckResult> CheckAsync(
        string tenant, string checkKey, CancellationToken cancellationToken = default) =>
        _runtime.CheckAsync(tenant, checkKey, cancellationToken);

    /// <summary>Runs every health probe and reports what they add up to.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The report.</returns>
    public Task<HealthReport> CheckHealthAsync(string tenant, CancellationToken cancellationToken = default) =>
        _runtime.CheckHealthAsync(tenant, cancellationToken);

    /// <summary>Reports the last known health without running any probe.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The report.</returns>
    public HealthReport LastHealthReport(string tenant) => _runtime.LastHealthReport(tenant);

    /// <summary>Gets the recorded history of a health check.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="checkKey">The check.</param>
    /// <returns>The history, newest last.</returns>
    public IReadOnlyList<HealthCheckResult> HealthHistory(string tenant, string checkKey) =>
        _runtime.HealthHistory(tenant, checkKey);

    /// <summary>Gets a value indicating whether a principal holds a monitoring right.</summary>
    /// <param name="permission">The right to test.</param>
    /// <param name="principals">The principal and any roles or groups it belongs to.</param>
    /// <returns><see langword="true"/> when the right is held.</returns>
    public bool Allows(MonitoringPermission permission, params string[] principals) =>
        _permissions.Allows(permission, principals);

    /// <summary>Reads the engine's own counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public MonitoringMetricsSnapshot Snapshot() => _metrics.Snapshot();
}
