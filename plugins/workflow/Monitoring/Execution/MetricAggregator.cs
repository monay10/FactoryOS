using FactoryOS.Plugins.Workflow.Monitoring.Domain;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>
/// Collapses the measurements in a window into one number.
/// <para>
/// Sampling weights are honoured wherever they change the answer: a total, a rate and a count all ask "how much
/// happened", so they read the weighted values, and a series sampled at one in ten reports the same total as
/// one recorded in full. A minimum, a maximum and a percentile ask "how extreme did it get", which is a
/// property of the measurements themselves, so they read the raw values.
/// </para>
/// </summary>
public sealed class MetricAggregator
{
    /// <summary>Collapses a series over a window.</summary>
    /// <param name="series">The series.</param>
    /// <param name="aggregation">How to collapse it.</param>
    /// <param name="fromUtc">The start of the window, inclusive.</param>
    /// <param name="toUtc">The end of the window, exclusive.</param>
    /// <returns>The snapshot.</returns>
    public MetricSnapshot Aggregate(
        MetricSeries series, MetricAggregation aggregation, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        ArgumentNullException.ThrowIfNull(series);
        return Aggregate(series.Instance, series.Window(fromUtc, toUtc), aggregation, fromUtc, toUtc);
    }

    /// <summary>Collapses a set of measurements over a window.</summary>
    /// <param name="instance">The series the measurements belong to.</param>
    /// <param name="values">The measurements in the window, in time order.</param>
    /// <param name="aggregation">How to collapse them.</param>
    /// <param name="fromUtc">The start of the window, inclusive.</param>
    /// <param name="toUtc">The end of the window, exclusive.</param>
    /// <returns>The snapshot.</returns>
    public MetricSnapshot Aggregate(
        MetricInstance instance,
        IReadOnlyList<MetricValue> values,
        MetricAggregation aggregation,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            return MetricSnapshot.Empty(instance, aggregation, fromUtc, toUtc);
        }

        var weightedSum = values.Sum(value => value.WeightedValue);
        var observations = values.Sum(value => value.Weight);
        var minimum = values.Min(value => value.Value);
        var maximum = values.Max(value => value.Value);

        var aggregated = aggregation switch
        {
            MetricAggregation.Sum => weightedSum,
            MetricAggregation.Count => observations,
            MetricAggregation.Average => weightedSum / observations,
            MetricAggregation.Minimum => minimum,
            MetricAggregation.Maximum => maximum,
            MetricAggregation.Last => values[^1].Value,
            MetricAggregation.Rate => RatePerSecond(weightedSum, toUtc - fromUtc),
            _ => Percentile(values, 0.95),
        };

        return new MetricSnapshot(
            instance,
            aggregation,
            aggregated,
            observations,
            weightedSum,
            minimum,
            maximum,
            fromUtc,
            toUtc,
            values[^1].Correlation);
    }

    /// <summary>
    /// Collapses a series into one snapshot per bucket — how a roll-up keeps the shape of old history at a
    /// coarser resolution than it was measured at.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="aggregation">How to collapse each bucket.</param>
    /// <param name="fromUtc">The start of the first bucket.</param>
    /// <param name="toUtc">The end of the last bucket, exclusive.</param>
    /// <param name="bucket">The bucket length.</param>
    /// <returns>The snapshots, in time order; empty buckets are omitted.</returns>
    public IReadOnlyList<MetricSnapshot> Bucket(
        MetricSeries series,
        MetricAggregation aggregation,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeSpan bucket)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bucket, TimeSpan.Zero);

        var snapshots = new List<MetricSnapshot>();
        for (var start = fromUtc; start < toUtc; start += bucket)
        {
            var end = start + bucket;
            var snapshot = Aggregate(series, aggregation, start, end > toUtc ? toUtc : end);
            if (!snapshot.IsEmpty)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    private static double RatePerSecond(double weightedSum, TimeSpan window) =>
        window <= TimeSpan.Zero ? 0 : weightedSum / window.TotalSeconds;

    // Nearest-rank on the raw measurements: the reported number is one that was actually observed, which is
    // what makes "the 95th percentile was 812 ms" a statement somebody can go and find in the trace.
    private static double Percentile(IReadOnlyList<MetricValue> values, double percentile)
    {
        var ordered = values.Select(value => value.Value).OrderBy(value => value).ToArray();
        var rank = (int)Math.Ceiling(percentile * ordered.Length);
        return ordered[Math.Clamp(rank - 1, 0, ordered.Length - 1)];
    }
}
