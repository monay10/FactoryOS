using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>What the collector did with a measurement.</summary>
/// <param name="Instance">The series it was offered to.</param>
/// <param name="Value">The value that was recorded, or <see langword="null"/> when it was sampled out.</param>
public sealed record MetricCollection(MetricInstance Instance, MetricValue? Value)
{
    /// <summary>Gets a value indicating whether the measurement was recorded.</summary>
    public bool WasRecorded => Value is not null;
}

/// <summary>
/// The way a measurement gets into the platform: resolve the definition, let the sampler rule on it, and
/// append it to its tenant's series.
/// <para>
/// An unregistered metric key is refused rather than accepted into an ad-hoc series. A metric nobody defined is
/// a typo far more often than it is a new measurement, and a typo that silently creates its own series is a
/// dashboard that quietly reads zero forever.
/// </para>
/// </summary>
public sealed class MetricCollector
{
    private readonly IMetricRepository _definitions;
    private readonly IMetricStore _store;
    private readonly MetricSampler _sampler;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="MetricCollector"/> class.</summary>
    /// <param name="definitions">The metric definition registry.</param>
    /// <param name="store">The series store.</param>
    /// <param name="sampler">The sampler.</param>
    /// <param name="clock">The clock, used when a measurement carries no timestamp.</param>
    public MetricCollector(
        IMetricRepository definitions, IMetricStore store, MetricSampler sampler, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(sampler);
        ArgumentNullException.ThrowIfNull(clock);
        _definitions = definitions;
        _store = store;
        _sampler = sampler;
        _clock = clock;
    }

    /// <summary>Records a measurement.</summary>
    /// <param name="tenant">The tenant the measurement belongs to.</param>
    /// <param name="metricKey">The metric being measured.</param>
    /// <param name="value">The measured number.</param>
    /// <param name="dimension">The labels slicing the series, or <see langword="null"/> for the unsliced one.</param>
    /// <param name="correlation">The operation the measurement came from.</param>
    /// <param name="timestampUtc">When it was measured; defaults to now.</param>
    /// <returns>What was done with the measurement.</returns>
    /// <exception cref="InvalidOperationException">The metric is not registered.</exception>
    public MetricCollection Record(
        string tenant,
        string metricKey,
        double value,
        MetricDimension? dimension = null,
        MetricCorrelation? correlation = null,
        DateTimeOffset? timestampUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricKey);

        var definition = _definitions.Find(metricKey)
            ?? throw new InvalidOperationException(
                $"Metric '{metricKey}' is not registered; register its definition before recording it.");

        var instance = MetricInstance.Of(tenant, metricKey, dimension);
        var measurement = new MetricValue(
            value, timestampUtc ?? _clock.UtcNow, correlation ?? MetricCorrelation.None);

        var admitted = _sampler.Sample(definition, instance, measurement);
        if (admitted is null)
        {
            return new MetricCollection(instance, null);
        }

        _store.GetOrCreate(instance, definition.Kind).Append(admitted);
        return new MetricCollection(instance, admitted);
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
}
