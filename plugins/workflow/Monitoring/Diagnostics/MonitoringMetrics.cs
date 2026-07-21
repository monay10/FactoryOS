using System.Threading;

namespace FactoryOS.Plugins.Workflow.Monitoring.Diagnostics;

/// <summary>An immutable snapshot of the monitoring engine's own counters.</summary>
/// <param name="Collected">How many measurements were admitted into a series.</param>
/// <param name="Sampled">How many measurements the sampler dropped.</param>
/// <param name="Aggregations">How many snapshots were produced.</param>
/// <param name="ThresholdBreaches">How many threshold evaluations came back outside their limits.</param>
/// <param name="AlertsTriggered">How many alerts opened.</param>
/// <param name="AlertsResolved">How many alerts closed.</param>
/// <param name="HealthChecks">How many probes were run.</param>
/// <param name="Expired">How many measurements outlived their retention.</param>
/// <param name="BridgeFaults">How many times a bridge failed to record what an engine event described.</param>
public sealed record MonitoringMetricsSnapshot(
    long Collected,
    long Sampled,
    long Aggregations,
    long ThresholdBreaches,
    long AlertsTriggered,
    long AlertsResolved,
    long HealthChecks,
    long Expired,
    long BridgeFaults);

/// <summary>
/// Thread-safe counters for the monitoring engine itself.
/// <para>
/// An observability layer that cannot be observed is a blind spot in exactly the place you least want one:
/// these counters are how you find out that the sampler is dropping most of what it is given, or that a
/// retention pass is falling behind. They are plain counters rather than metrics in the store, so the engine
/// can always report on itself even when the store is the thing that is wrong.
/// </para>
/// </summary>
public sealed class MonitoringMetrics
{
    private long _collected;
    private long _sampled;
    private long _aggregations;
    private long _thresholdBreaches;
    private long _alertsTriggered;
    private long _alertsResolved;
    private long _healthChecks;
    private long _expired;
    private long _bridgeFaults;

    /// <summary>Records that a measurement was admitted into a series.</summary>
    public void RecordCollected() => Interlocked.Increment(ref _collected);

    /// <summary>Records that the sampler dropped a measurement.</summary>
    public void RecordSampled() => Interlocked.Increment(ref _sampled);

    /// <summary>Records that a snapshot was produced.</summary>
    /// <param name="count">How many.</param>
    public void RecordAggregations(int count) => Interlocked.Add(ref _aggregations, count);

    /// <summary>Records that a threshold evaluation came back outside its limits.</summary>
    /// <param name="count">How many.</param>
    public void RecordThresholdBreaches(int count) => Interlocked.Add(ref _thresholdBreaches, count);

    /// <summary>Records that alerts opened.</summary>
    /// <param name="count">How many.</param>
    public void RecordAlertsTriggered(int count) => Interlocked.Add(ref _alertsTriggered, count);

    /// <summary>Records that alerts closed.</summary>
    /// <param name="count">How many.</param>
    public void RecordAlertsResolved(int count) => Interlocked.Add(ref _alertsResolved, count);

    /// <summary>Records that a probe was run.</summary>
    public void RecordHealthCheck() => Interlocked.Increment(ref _healthChecks);

    /// <summary>Records that several probes were run.</summary>
    /// <param name="count">How many.</param>
    public void RecordHealthChecks(int count) => Interlocked.Add(ref _healthChecks, count);

    /// <summary>Records that measurements outlived their retention.</summary>
    /// <param name="count">How many.</param>
    public void RecordExpired(int count) => Interlocked.Add(ref _expired, count);

    /// <summary>
    /// Records that a bridge could not turn an engine event into a measurement. The bridge contains the
    /// failure so the engine it observes is unaffected — this counter is what stops that containment from
    /// turning into silence.
    /// </summary>
    public void RecordBridgeFault() => Interlocked.Increment(ref _bridgeFaults);

    /// <summary>Reads the current counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public MonitoringMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _collected),
        Interlocked.Read(ref _sampled),
        Interlocked.Read(ref _aggregations),
        Interlocked.Read(ref _thresholdBreaches),
        Interlocked.Read(ref _alertsTriggered),
        Interlocked.Read(ref _alertsResolved),
        Interlocked.Read(ref _healthChecks),
        Interlocked.Read(ref _expired),
        Interlocked.Read(ref _bridgeFaults));
}
