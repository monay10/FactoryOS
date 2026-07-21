using FactoryOS.Plugins.Workflow.Monitoring.Configuration;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>What one retention pass did.</summary>
/// <param name="Tenant">The tenant the pass ran for.</param>
/// <param name="Removed">How many raw measurements left the store.</param>
/// <param name="RolledUp">How many aggregated values replaced them.</param>
/// <param name="SeriesTouched">How many series the pass changed.</param>
public sealed record MetricRetentionSummary(string Tenant, int Removed, int RolledUp, int SeriesTouched)
{
    /// <summary>Gets a value indicating whether the pass changed anything at all.</summary>
    public bool ChangedAnything => Removed > 0 || RolledUp > 0;
}

/// <summary>
/// Applies retention to a tenant's series: old measurements are either dropped or collapsed into a coarser
/// roll-up, according to the most specific policy that matches the metric.
/// <para>
/// There is always an answer. A metric with no matching policy falls back to the engine's default retention,
/// because the alternative — keeping everything for a metric nobody wrote a policy for — is how an
/// observability store outgrows the system it was meant to observe.
/// </para>
/// </summary>
public sealed class MetricRetentionManager
{
    private readonly IMetricRepository _definitions;
    private readonly IMetricStore _store;
    private readonly MetricAggregator _aggregator;
    private readonly MonitoringEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="MetricRetentionManager"/> class.</summary>
    /// <param name="definitions">The definition and policy registry.</param>
    /// <param name="store">The series store.</param>
    /// <param name="aggregator">The aggregator used to collapse roll-up buckets.</param>
    /// <param name="options">The engine options carrying the default retention.</param>
    public MetricRetentionManager(
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

    /// <summary>Runs retention over a tenant's series.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="nowUtc">The instant retention is measured back from.</param>
    /// <returns>What the pass did.</returns>
    public MetricRetentionSummary Run(string tenant, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var policies = _definitions.RetentionPolicies();
        var budget = _options.MaintenanceBatchSize;
        var removed = 0;
        var rolledUp = 0;
        var touched = 0;

        foreach (var series in _store.ListByTenant(tenant))
        {
            if (budget <= 0)
            {
                break;
            }

            var definition = _definitions.Find(series.MetricKey);
            if (definition is null)
            {
                continue;
            }

            var policy = PolicyFor(definition, policies);
            var cutoff = nowUtc - policy.RetainRaw;

            if (policy.Action == MetricRetentionAction.RollUp)
            {
                var (replaced, produced) = RollUp(series, policy, cutoff);
                if (replaced == 0)
                {
                    continue;
                }

                removed += replaced;
                rolledUp += produced;
                budget -= replaced;
                touched++;
                continue;
            }

            var dropped = series.RemoveBefore(cutoff, budget);
            if (dropped == 0)
            {
                continue;
            }

            removed += dropped;
            budget -= dropped;
            touched++;
        }

        return new MetricRetentionSummary(tenant, removed, rolledUp, touched);
    }

    /// <summary>Gets the policy that governs a metric — the most specific match, or the engine default.</summary>
    /// <param name="definition">The metric definition.</param>
    /// <param name="policies">The registered policies.</param>
    /// <returns>The governing policy.</returns>
    public MetricRetentionPolicy PolicyFor(
        MetricDefinition definition, IReadOnlyList<MetricRetentionPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(policies);

        return policies
            .Where(policy => policy.Matches(definition))
            .OrderByDescending(policy => policy.Specificity)
            .FirstOrDefault()
            ?? new MetricRetentionPolicy(_options.DefaultRetention, MetricRetentionAction.Delete);
    }

    private (int Replaced, int Produced) RollUp(
        MetricSeries series, MetricRetentionPolicy policy, DateTimeOffset cutoff)
    {
        // What is replaced is what falls strictly before the cutoff, which is exactly what ReplaceBefore keeps
        // out. Reading the aged set with the same boundary is what stops a measurement sitting on the cutoff
        // from being rolled up and kept at the same time — and so counted twice ever after.
        var agedEnd = cutoff.AddTicks(-1);
        var aged = series.Window(DateTimeOffset.MinValue, agedEnd);
        if (aged.Count == 0)
        {
            return (0, 0);
        }

        var buckets = _aggregator.Bucket(
            series, policy.RollUpUsing, Floor(aged[0].TimestampUtc, policy.RollUpBucket), agedEnd,
            policy.RollUpBucket);

        var replacements = buckets
            .Select(snapshot => new MetricValue(
                snapshot.Value, snapshot.FromUtc, snapshot.Correlation, WeightOf(policy.RollUpUsing, snapshot)))
            .ToArray();

        var replaced = series.ReplaceBefore(cutoff, replacements);
        return (replaced, replacements.Length);
    }

    /// <summary>
    /// Decides what a rolled-up value stands for. A sum, a count or a rate has already absorbed everything in
    /// its bucket, so it weighs one; an average, a minimum or a percentile is one number drawn from many, so it
    /// carries the count behind it and a later average over the roll-up still lands where it should.
    /// </summary>
    private static int WeightOf(MetricAggregation aggregation, MetricSnapshot snapshot) =>
        aggregation is MetricAggregation.Sum or MetricAggregation.Count or MetricAggregation.Rate
            ? 1
            : Math.Max(1, snapshot.Count);

    // The bucket floor is stepped back one tick because windows exclude their start: without it the earliest
    // measurement would sit exactly on the first bucket's boundary and fall out of every bucket.
    private static DateTimeOffset Floor(DateTimeOffset instant, TimeSpan bucket) =>
        new DateTimeOffset(instant.Ticks - (instant.Ticks % bucket.Ticks), instant.Offset).AddTicks(-1);
}
