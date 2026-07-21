using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;

namespace FactoryOS.Plugins.Workflow.Monitoring.Persistence;

/// <summary>
/// The registry of what the platform measures and what it expects: metric definitions, thresholds, alert rules
/// and retention policies. This is configuration, and it is the same for every tenant — tenants differ in the
/// values they produce, never in what a metric means.
/// </summary>
public interface IMetricRepository
{
    /// <summary>Registers a metric definition, replacing any definition with the same key.</summary>
    /// <param name="definition">The definition.</param>
    void Register(MetricDefinition definition);

    /// <summary>Registers a threshold, replacing any threshold with the same key.</summary>
    /// <param name="threshold">The threshold.</param>
    void RegisterThreshold(MetricThreshold threshold);

    /// <summary>Registers an alert rule, replacing any rule with the same key.</summary>
    /// <param name="rule">The rule.</param>
    void RegisterAlertRule(MetricAlertRule rule);

    /// <summary>Registers a retention policy.</summary>
    /// <param name="policy">The policy.</param>
    void RegisterRetention(MetricRetentionPolicy policy);

    /// <summary>Gets a definition by key.</summary>
    /// <param name="key">The metric key.</param>
    /// <returns>The definition, or <see langword="null"/> when the metric is not registered.</returns>
    MetricDefinition? Find(string key);

    /// <summary>Gets every registered definition.</summary>
    /// <returns>The definitions.</returns>
    IReadOnlyList<MetricDefinition> Definitions();

    /// <summary>Gets every registered threshold.</summary>
    /// <returns>The thresholds.</returns>
    IReadOnlyList<MetricThreshold> Thresholds();

    /// <summary>Gets the thresholds watching a metric.</summary>
    /// <param name="metricKey">The metric key.</param>
    /// <returns>The thresholds.</returns>
    IReadOnlyList<MetricThreshold> ThresholdsFor(string metricKey);

    /// <summary>Gets every registered alert rule.</summary>
    /// <returns>The rules.</returns>
    IReadOnlyList<MetricAlertRule> AlertRules();

    /// <summary>Gets every registered retention policy.</summary>
    /// <returns>The policies.</returns>
    IReadOnlyList<MetricRetentionPolicy> RetentionPolicies();
}

/// <summary>An in-memory <see cref="IMetricRepository"/>.</summary>
public sealed class InMemoryMetricRepository : IMetricRepository
{
    private readonly ConcurrentDictionary<string, MetricDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MetricThreshold> _thresholds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MetricAlertRule> _rules = new(StringComparer.Ordinal);
    private readonly List<MetricRetentionPolicy> _retention = [];
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public void Register(MetricDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Key] = definition;
    }

    /// <inheritdoc />
    public void RegisterThreshold(MetricThreshold threshold)
    {
        ArgumentNullException.ThrowIfNull(threshold);
        _thresholds[threshold.Key] = threshold;
    }

    /// <inheritdoc />
    public void RegisterAlertRule(MetricAlertRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules[rule.Key] = rule;
    }

    /// <inheritdoc />
    public void RegisterRetention(MetricRetentionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        lock (_gate)
        {
            _retention.Add(policy);
        }
    }

    /// <inheritdoc />
    public MetricDefinition? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryGetValue(key, out var definition) ? definition : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<MetricDefinition> Definitions() =>
        _definitions.Values.OrderBy(definition => definition.Key, StringComparer.Ordinal).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<MetricThreshold> Thresholds() =>
        _thresholds.Values.OrderBy(threshold => threshold.Key, StringComparer.Ordinal).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<MetricThreshold> ThresholdsFor(string metricKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricKey);
        return _thresholds.Values
            .Where(threshold => string.Equals(threshold.MetricKey, metricKey, StringComparison.Ordinal))
            .OrderBy(threshold => threshold.Key, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<MetricAlertRule> AlertRules() =>
        _rules.Values.OrderBy(rule => rule.Key, StringComparer.Ordinal).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<MetricRetentionPolicy> RetentionPolicies()
    {
        lock (_gate)
        {
            return _retention.ToArray();
        }
    }
}

/// <summary>
/// The store of measured series. Every operation names a tenant, and there is no operation that spans them:
/// one factory's numbers are never visible in another's dashboard, by construction rather than by filter.
/// </summary>
public interface IMetricStore
{
    /// <summary>Gets the series for an instance, creating it when this is its first measurement.</summary>
    /// <param name="instance">The series identity.</param>
    /// <param name="kind">What kind of quantity the series carries.</param>
    /// <returns>The series.</returns>
    MetricSeries GetOrCreate(MetricInstance instance, MetricKind kind);

    /// <summary>Finds a series, without creating one.</summary>
    /// <param name="instance">The series identity.</param>
    /// <returns>The series, or <see langword="null"/> when nothing was ever measured for it.</returns>
    MetricSeries? Find(MetricInstance instance);

    /// <summary>Lists a tenant's series.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The series.</returns>
    IReadOnlyList<MetricSeries> ListByTenant(string tenant);

    /// <summary>Lists every series of one metric within a tenant — one per dimension.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="metricKey">The metric key.</param>
    /// <returns>The series.</returns>
    IReadOnlyList<MetricSeries> ListByMetric(string tenant, string metricKey);

    /// <summary>Gets the tenants that have measured anything.</summary>
    /// <returns>The tenants.</returns>
    IReadOnlyList<string> Tenants();
}

/// <summary>An in-memory <see cref="IMetricStore"/>.</summary>
public sealed class InMemoryMetricStore : IMetricStore
{
    private readonly ConcurrentDictionary<string, MetricSeries> _series = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public MetricSeries GetOrCreate(MetricInstance instance, MetricKind kind)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return _series.GetOrAdd(instance.Key, _ => new MetricSeries(instance, kind));
    }

    /// <inheritdoc />
    public MetricSeries? Find(MetricInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return _series.TryGetValue(instance.Key, out var series) ? series : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<MetricSeries> ListByTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _series.Values
            .Where(series => string.Equals(series.Tenant, tenant, StringComparison.Ordinal))
            .OrderBy(series => series.Instance.Key, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<MetricSeries> ListByMetric(string tenant, string metricKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricKey);
        return _series.Values
            .Where(series => string.Equals(series.Tenant, tenant, StringComparison.Ordinal)
                && string.Equals(series.MetricKey, metricKey, StringComparison.Ordinal))
            .OrderBy(series => series.Instance.Dimension.Key, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Tenants() => _series.Values
        .Select(series => series.Tenant)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(tenant => tenant, StringComparer.Ordinal)
        .ToArray();
}

/// <summary>The registry of the components the platform can be asked about.</summary>
public interface IHealthRepository
{
    /// <summary>Registers a check, replacing any check with the same key.</summary>
    /// <param name="check">The check.</param>
    void Register(HealthCheck check);

    /// <summary>Gets a check by key.</summary>
    /// <param name="key">The check key.</param>
    /// <returns>The check, or <see langword="null"/> when it is not registered.</returns>
    HealthCheck? Find(string key);

    /// <summary>Gets every registered check.</summary>
    /// <returns>The checks.</returns>
    IReadOnlyList<HealthCheck> Checks();
}

/// <summary>An in-memory <see cref="IHealthRepository"/>.</summary>
public sealed class InMemoryHealthRepository : IHealthRepository
{
    private readonly ConcurrentDictionary<string, HealthCheck> _checks = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(HealthCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        _checks[check.Key] = check;
    }

    /// <inheritdoc />
    public HealthCheck? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _checks.TryGetValue(key, out var check) ? check : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthCheck> Checks() =>
        _checks.Values.OrderBy(check => check.Key, StringComparer.Ordinal).ToArray();
}

/// <summary>
/// The store of what the probes have found. It keeps the latest result per tenant and check — which is what
/// makes a status <i>change</i> detectable — and a bounded history behind it.
/// </summary>
public interface IHealthStore
{
    /// <summary>Records a result and returns the one it replaced.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="result">The result.</param>
    /// <returns>The previous result for the same check, or <see langword="null"/> when it is the first.</returns>
    HealthCheckResult? Append(string tenant, HealthCheckResult result);

    /// <summary>Gets the latest result for a check.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="checkKey">The check.</param>
    /// <returns>The result, or <see langword="null"/> when the check has never run.</returns>
    HealthCheckResult? Latest(string tenant, string checkKey);

    /// <summary>Gets the latest result of every check that has run for a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The results.</returns>
    IReadOnlyList<HealthCheckResult> LatestAll(string tenant);

    /// <summary>Gets the recorded history of a check, newest last.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="checkKey">The check.</param>
    /// <returns>The history.</returns>
    IReadOnlyList<HealthCheckResult> History(string tenant, string checkKey);
}

/// <summary>
/// An in-memory <see cref="IHealthStore"/> keeping a bounded history per check. The bound is deliberate: health
/// history is diagnostic, and an unbounded diagnostic buffer is how an observability layer becomes the outage.
/// </summary>
public sealed class InMemoryHealthStore : IHealthStore
{
    private const int HistoryLimit = 100;

    private readonly ConcurrentDictionary<string, List<HealthCheckResult>> _history = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public HealthCheckResult? Append(string tenant, HealthCheckResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(result);

        var entries = _history.GetOrAdd(Key(tenant, result.Key), _ => []);
        lock (entries)
        {
            var previous = entries.Count == 0 ? null : entries[^1];
            entries.Add(result);
            if (entries.Count > HistoryLimit)
            {
                entries.RemoveRange(0, entries.Count - HistoryLimit);
            }

            return previous;
        }
    }

    /// <inheritdoc />
    public HealthCheckResult? Latest(string tenant, string checkKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkKey);

        if (!_history.TryGetValue(Key(tenant, checkKey), out var entries))
        {
            return null;
        }

        lock (entries)
        {
            return entries.Count == 0 ? null : entries[^1];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthCheckResult> LatestAll(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        var prefix = tenant + "|";
        return _history
            .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(pair =>
            {
                lock (pair.Value)
                {
                    return pair.Value.Count == 0 ? null : pair.Value[^1];
                }
            })
            .OfType<HealthCheckResult>()
            .OrderBy(result => result.Key, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthCheckResult> History(string tenant, string checkKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkKey);

        if (!_history.TryGetValue(Key(tenant, checkKey), out var entries))
        {
            return [];
        }

        lock (entries)
        {
            return entries.ToArray();
        }
    }

    private static string Key(string tenant, string checkKey) => $"{tenant}|{checkKey}";
}
