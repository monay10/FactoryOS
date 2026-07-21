namespace FactoryOS.Plugins.Workflow.Monitoring.Domain;

/// <summary>
/// What a series looked like over a window, once collapsed into numbers a human or a threshold can act on.
/// A snapshot always names the window it covers: a metric value without its window is not a measurement, it is
/// a rumour.
/// </summary>
/// <param name="Instance">The series the snapshot describes.</param>
/// <param name="Aggregation">The aggregation <see cref="Value"/> was produced by.</param>
/// <param name="Value">The aggregated number.</param>
/// <param name="Count">How many measurements the window held.</param>
/// <param name="Sum">The total of the measurements, weighted for sampling.</param>
/// <param name="Minimum">The smallest measurement, or <see langword="null"/> when the window was empty.</param>
/// <param name="Maximum">The largest measurement, or <see langword="null"/> when the window was empty.</param>
/// <param name="FromUtc">The start of the window, exclusive.</param>
/// <param name="ToUtc">The end of the window, inclusive.</param>
/// <param name="Correlation">
/// The correlation of the most recent measurement in the window, so a snapshot that trips a threshold can name
/// the operation that tripped it.
/// </param>
public sealed record MetricSnapshot(
    MetricInstance Instance,
    MetricAggregation Aggregation,
    double Value,
    int Count,
    double Sum,
    double? Minimum,
    double? Maximum,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    MetricCorrelation Correlation)
{
    /// <summary>Gets a value indicating whether the window held no measurements at all.</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>Gets the length of the window.</summary>
    public TimeSpan Window => ToUtc - FromUtc;

    /// <summary>Creates the snapshot of an empty window.</summary>
    /// <param name="instance">The series.</param>
    /// <param name="aggregation">The aggregation that was asked for.</param>
    /// <param name="fromUtc">The start of the window.</param>
    /// <param name="toUtc">The end of the window.</param>
    /// <returns>The snapshot.</returns>
    public static MetricSnapshot Empty(
        MetricInstance instance, MetricAggregation aggregation, DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        new(instance, aggregation, 0, 0, 0, null, null, fromUtc, toUtc, MetricCorrelation.None);
}
