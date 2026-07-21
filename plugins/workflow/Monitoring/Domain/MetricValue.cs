namespace FactoryOS.Plugins.Workflow.Monitoring.Domain;

/// <summary>
/// The identity of one time series: a metric, in a tenant, sliced by a dimension. Everything the store holds
/// is keyed by an instance, and the tenant is part of that key — there is no way to read or write a series
/// without naming whose it is.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="MetricKey">The metric definition key.</param>
/// <param name="Dimension">The labels separating this series from its siblings.</param>
public sealed record MetricInstance(string Tenant, string MetricKey, MetricDimension Dimension)
{
    /// <summary>Creates an instance, validating the tenant and key.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="metricKey">The metric key.</param>
    /// <param name="dimension">The dimension, or <see langword="null"/> for the unsliced series.</param>
    /// <returns>The instance.</returns>
    public static MetricInstance Of(string tenant, string metricKey, MetricDimension? dimension = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricKey);
        return new MetricInstance(tenant, metricKey, dimension ?? MetricDimension.None);
    }

    /// <summary>Gets the stable identity of the series, safe to use as a storage key.</summary>
    public string Key => $"{Tenant}|{MetricKey}|{Dimension.Key}";
}

/// <summary>
/// One measurement: a number, when it was taken, and the operation it came from. Values are immutable — a
/// measurement is a statement about a moment that has already passed, so there is nothing about it to change.
/// </summary>
/// <param name="Value">The measured number.</param>
/// <param name="TimestampUtc">When the measurement was taken.</param>
/// <param name="Correlation">The identifiers tying it back to the operation that produced it.</param>
/// <param name="Weight">
/// How many original observations this value stands for. A value the sampler let through unchanged weighs one;
/// a sampled counter weighs the whole run it represents, which is what keeps totals honest.
/// </param>
public sealed record MetricValue(
    double Value,
    DateTimeOffset TimestampUtc,
    MetricCorrelation Correlation,
    int Weight = 1)
{
    /// <summary>Creates a value with no correlation context.</summary>
    /// <param name="value">The measured number.</param>
    /// <param name="timestampUtc">When it was taken.</param>
    /// <returns>The value.</returns>
    public static MetricValue At(double value, DateTimeOffset timestampUtc) =>
        new(value, timestampUtc, MetricCorrelation.None);

    /// <summary>Gets the value's contribution to a total, accounting for sampling.</summary>
    public double WeightedValue => Value * Weight;
}
