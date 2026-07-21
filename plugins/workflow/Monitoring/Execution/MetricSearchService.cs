using FactoryOS.Plugins.Workflow.Monitoring.Configuration;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>
/// The filters that select what to read out of the metric store. The tenant is required and every other filter
/// narrows within it — there is no query shape that can read across tenants.
/// </summary>
/// <param name="Tenant">The tenant to read.</param>
public sealed record MetricQuery(string Tenant)
{
    /// <summary>Gets the exact metric to read, or <see langword="null"/> for any.</summary>
    public string? MetricKey { get; init; }

    /// <summary>Gets the metric key prefix to match, or <see langword="null"/> for any.</summary>
    public string? KeyPrefix { get; init; }

    /// <summary>Gets the category to read, or <see langword="null"/> for any.</summary>
    public MetricCategory? Category { get; init; }

    /// <summary>Gets the labels a series must carry, or <see langword="null"/> for any.</summary>
    public MetricDimension? Dimension { get; init; }

    /// <summary>Gets the start of the window, or <see langword="null"/> for everything retained.</summary>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>Gets the end of the window, or <see langword="null"/> for now.</summary>
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>Gets the aggregation applied, or <see langword="null"/> for each metric's default.</summary>
    public MetricAggregation? Aggregation { get; init; }

    /// <summary>Gets the maximum number of series returned.</summary>
    public int Limit { get; init; } = 500;
}

/// <summary>
/// Reads the metric store: which series exist, what they add up to over a window, and — the question that
/// makes an alert actionable — which measurements a given operation produced.
/// </summary>
public sealed class MetricSearchService
{
    private readonly IMetricStore _store;
    private readonly IMetricRepository _definitions;
    private readonly MetricAggregator _aggregator;
    private readonly MonitoringEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="MetricSearchService"/> class.</summary>
    /// <param name="store">The series store.</param>
    /// <param name="definitions">The definition registry.</param>
    /// <param name="aggregator">The aggregator.</param>
    /// <param name="options">The engine options carrying the default window.</param>
    public MetricSearchService(
        IMetricStore store,
        IMetricRepository definitions,
        MetricAggregator aggregator,
        MonitoringEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(options);
        _store = store;
        _definitions = definitions;
        _aggregator = aggregator;
        _options = options;
    }

    /// <summary>Finds the series a query selects.</summary>
    /// <param name="query">The filters.</param>
    /// <returns>The matching series.</returns>
    public IReadOnlyList<MetricSeries> Series(MetricQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _store.ListByTenant(query.Tenant)
            .Where(series => Matches(query, series))
            .Take(query.Limit)
            .ToArray();
    }

    /// <summary>Collapses the series a query selects into one snapshot each.</summary>
    /// <param name="query">The filters.</param>
    /// <param name="nowUtc">The instant used when the query names no end.</param>
    /// <returns>The snapshots, empty windows omitted.</returns>
    public IReadOnlyList<MetricSnapshot> Search(MetricQuery query, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(query);

        var to = query.ToUtc ?? nowUtc;
        var from = query.FromUtc ?? to - _options.DefaultWindow;

        return Series(query)
            .Select(series =>
            {
                var aggregation = query.Aggregation
                    ?? _definitions.Find(series.MetricKey)?.DefaultAggregation
                    ?? MetricAggregation.Sum;
                return _aggregator.Aggregate(series, aggregation, from, to);
            })
            .Where(snapshot => !snapshot.IsEmpty)
            .ToArray();
    }

    /// <summary>
    /// Finds every measurement a single operation produced, across every metric it touched — the query that
    /// turns "this alert fired" into "here is the request that caused it".
    /// </summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="correlationId">The correlation id to trace.</param>
    /// <returns>The measurements, with the series each belongs to, in time order.</returns>
    public IReadOnlyList<(MetricInstance Instance, MetricValue Value)> ByCorrelation(
        string tenant, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return _store.ListByTenant(tenant)
            .SelectMany(series => series.Values().Select(value => (series.Instance, Value: value)))
            .Where(pair => string.Equals(
                pair.Value.Correlation.CorrelationId, correlationId, StringComparison.Ordinal))
            .OrderBy(pair => pair.Value.TimestampUtc)
            .ToArray();
    }

    /// <summary>Finds every measurement belonging to one distributed trace.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="traceId">The trace id.</param>
    /// <returns>The measurements, with the series each belongs to, in time order.</returns>
    public IReadOnlyList<(MetricInstance Instance, MetricValue Value)> ByTrace(string tenant, string traceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);

        return _store.ListByTenant(tenant)
            .SelectMany(series => series.Values().Select(value => (series.Instance, Value: value)))
            .Where(pair => string.Equals(pair.Value.Correlation.TraceId, traceId, StringComparison.Ordinal))
            .OrderBy(pair => pair.Value.TimestampUtc)
            .ToArray();
    }

    private bool Matches(MetricQuery query, MetricSeries series)
    {
        if (query.MetricKey is not null
            && !string.Equals(query.MetricKey, series.MetricKey, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.KeyPrefix is not null
            && !series.MetricKey.StartsWith(query.KeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.Category is { } category && _definitions.Find(series.MetricKey)?.Category != category)
        {
            return false;
        }

        return query.Dimension is null || series.Instance.Dimension.Covers(query.Dimension);
    }
}
