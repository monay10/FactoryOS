using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>
/// What a health probe is given to work with: the tenant it is answering for, the instant it is answering at,
/// the window it should judge over, and read access to the measurements it judges.
/// <para>
/// A probe reads; it never calls into the engine it reports on. That is what keeps the dependency arrow
/// pointing one way — monitoring learns about the workflow runtime from the measurements the runtime's events
/// produced, not by holding a reference to it and asking.
/// </para>
/// </summary>
public sealed class HealthProbeContext
{
    private readonly IMetricStore _store;
    private readonly MetricAggregator _aggregator;
    private readonly IMetricRepository _definitions;

    /// <summary>Initializes a new instance of the <see cref="HealthProbeContext"/> class.</summary>
    /// <param name="tenant">The tenant being answered for.</param>
    /// <param name="nowUtc">The instant being answered at.</param>
    /// <param name="window">The window signals are judged over.</param>
    /// <param name="store">The series store.</param>
    /// <param name="aggregator">The aggregator.</param>
    /// <param name="definitions">The definition registry.</param>
    public HealthProbeContext(
        string tenant,
        DateTimeOffset nowUtc,
        TimeSpan window,
        IMetricStore store,
        MetricAggregator aggregator,
        IMetricRepository definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(definitions);

        Tenant = tenant;
        NowUtc = nowUtc;
        Window = window;
        _store = store;
        _aggregator = aggregator;
        _definitions = definitions;
    }

    /// <summary>Gets the tenant being answered for.</summary>
    public string Tenant { get; }

    /// <summary>Gets the instant being answered at.</summary>
    public DateTimeOffset NowUtc { get; }

    /// <summary>Gets the window signals are judged over.</summary>
    public TimeSpan Window { get; }

    /// <summary>Gets the start of the window.</summary>
    public DateTimeOffset FromUtc => NowUtc - Window;

    /// <summary>
    /// Sums a metric across every dimension in the window — how many times something happened, whatever it
    /// happened to.
    /// </summary>
    /// <param name="metricKey">The metric.</param>
    /// <returns>The total, honouring sampling weights.</returns>
    public double Total(string metricKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricKey);
        return _store.ListByMetric(Tenant, metricKey)
            .Sum(series => series.Window(FromUtc, NowUtc).Sum(value => value.WeightedValue));
    }

    /// <summary>Collapses one metric's series over the window, per dimension.</summary>
    /// <param name="metricKey">The metric.</param>
    /// <param name="aggregation">The aggregation, or <see langword="null"/> for the definition's default.</param>
    /// <returns>One snapshot per dimension; empty when the metric was never measured.</returns>
    public IReadOnlyList<MetricSnapshot> Snapshots(string metricKey, MetricAggregation? aggregation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricKey);
        var definition = _definitions.Find(metricKey);
        if (definition is null)
        {
            return [];
        }

        return _store.ListByMetric(Tenant, metricKey)
            .Select(series => _aggregator.Aggregate(
                series, aggregation ?? definition.DefaultAggregation, FromUtc, NowUtc))
            .Where(snapshot => !snapshot.IsEmpty)
            .ToArray();
    }

    /// <summary>Gets the largest value a metric reached across its dimensions in the window.</summary>
    /// <param name="metricKey">The metric.</param>
    /// <param name="aggregation">The aggregation applied per dimension first.</param>
    /// <returns>The worst value, or <see langword="null"/> when the metric was never measured.</returns>
    public double? Worst(string metricKey, MetricAggregation aggregation)
    {
        var snapshots = Snapshots(metricKey, aggregation);
        return snapshots.Count == 0 ? null : snapshots.Max(snapshot => snapshot.Value);
    }

    /// <summary>Gets when a metric was last measured, across every dimension.</summary>
    /// <param name="metricKey">The metric.</param>
    /// <returns>The instant, or <see langword="null"/> when the metric was never measured.</returns>
    public DateTimeOffset? LastMeasuredAt(string metricKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricKey);
        var timestamps = _store.ListByMetric(Tenant, metricKey)
            .Select(series => series.Latest()?.TimestampUtc)
            .OfType<DateTimeOffset>()
            .ToArray();
        return timestamps.Length == 0 ? null : timestamps.Max();
    }
}

/// <summary>Answers what state a component is in.</summary>
/// <param name="context">What the probe has to work with.</param>
/// <param name="cancellationToken">Cancels a probe that is taking too long.</param>
/// <returns>What the probe found.</returns>
public delegate ValueTask<HealthCheckResult> HealthProbe(
    HealthProbeContext context, CancellationToken cancellationToken);

/// <summary>
/// Pairs each registered <see cref="HealthCheck"/> with the probe that answers for it. The description of a
/// component is durable configuration and lives in the repository; the probe is an in-process delegate and
/// lives here, so how a component is measured can change without redefining what the component is.
/// </summary>
public sealed class HealthRegistry
{
    private readonly IHealthRepository _repository;
    private readonly ConcurrentDictionary<string, HealthProbe> _probes = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="HealthRegistry"/> class.</summary>
    /// <param name="repository">The registry of check descriptions.</param>
    public HealthRegistry(IHealthRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>Registers a check and the probe that answers for it, replacing any check with the same key.</summary>
    /// <param name="check">The check.</param>
    /// <param name="probe">The probe.</param>
    public void Register(HealthCheck check, HealthProbe probe)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(probe);
        _repository.Register(check);
        _probes[check.Key] = probe;
    }

    /// <summary>Gets the registered checks.</summary>
    /// <returns>The checks.</returns>
    public IReadOnlyList<HealthCheck> Checks() => _repository.Checks();

    /// <summary>Gets a check and its probe.</summary>
    /// <param name="key">The check key.</param>
    /// <returns>The pair, or <see langword="null"/> when the check is not registered.</returns>
    public (HealthCheck Check, HealthProbe Probe)? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var check = _repository.Find(key);
        return check is not null && _probes.TryGetValue(key, out var probe) ? (check, probe) : null;
    }

    /// <summary>Gets every check paired with its probe, in key order.</summary>
    /// <returns>The pairs.</returns>
    public IReadOnlyList<(HealthCheck Check, HealthProbe Probe)> All() => _repository.Checks()
        .Select(check => _probes.TryGetValue(check.Key, out var probe) ? (Check: check, Probe: probe) : default)
        .Where(pair => pair.Probe is not null)
        .ToArray();
}
