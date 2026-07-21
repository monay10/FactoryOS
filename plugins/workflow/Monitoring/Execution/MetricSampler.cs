using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>
/// Decides which measurements are kept when a metric is too hot to record in full.
/// <para>
/// Two properties matter more than cleverness here. First, the sampler is <b>deterministic</b>: it keeps one in
/// every N measurements of a series rather than rolling dice, so the same traffic always produces the same
/// series and a test can assert on it. Second, it is <b>kind-aware</b>: dropping four of five gauge readings
/// loses nothing an average cares about, but dropping four of five counter increments would understate a total
/// by eighty percent, so a kept counter measurement carries the weight of the run it stands for. Sampling may
/// cost resolution; it may never cost correctness.
/// </para>
/// </summary>
public sealed class MetricSampler
{
    private readonly ConcurrentDictionary<string, long> _counters = new(StringComparer.Ordinal);

    /// <summary>Decides whether a measurement is kept, and with what weight.</summary>
    /// <param name="definition">The metric definition, carrying its sample rate and kind.</param>
    /// <param name="instance">The series being measured.</param>
    /// <param name="value">The measurement.</param>
    /// <returns>The value to record, or <see langword="null"/> when this measurement is sampled out.</returns>
    public MetricValue? Sample(MetricDefinition definition, MetricInstance instance, MetricValue value)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(value);

        var interval = IntervalOf(definition.SampleRate);
        if (interval == 1)
        {
            return value;
        }

        // The counter is per series, so a rarely-used dimension is not starved by a busy sibling.
        var position = _counters.AddOrUpdate(instance.Key, 1, (_, current) => current + 1);
        if (position % interval != 0)
        {
            return null;
        }

        // A counter stands in for the measurements that were dropped; a gauge or a duration stands only for
        // itself, and inflating it would turn a sampled average into a fabricated one.
        return definition.Kind == MetricKind.Counter ? value with { Weight = interval } : value;
    }

    /// <summary>Gets how many measurements a kept one stands for, at a sample rate.</summary>
    /// <param name="sampleRate">The fraction of measurements kept.</param>
    /// <returns>The sampling interval, at least one.</returns>
    public static int IntervalOf(double sampleRate) => sampleRate >= 1
        ? 1
        : Math.Max(1, (int)Math.Round(1 / sampleRate, MidpointRounding.AwayFromZero));
}
