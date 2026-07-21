using FactoryOS.Plugins.Workflow.Monitoring.Diagnostics;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;

namespace FactoryOS.Plugins.Workflow.Monitoring.Bridge;

/// <summary>
/// Remembers when something started so its duration can be measured when it finishes.
/// <para>
/// Counting events tells you how much happened; only a duration tells you whether it happened <i>well</i>, and
/// the engines publish a start event and an end event without ever relating the two. Pairing them is the
/// bridge's job, and it is bounded on purpose: work that never finishes would otherwise accumulate for as long
/// as the process lives, which is how a monitoring layer becomes the outage it was installed to catch.
/// </para>
/// </summary>
public sealed class EventDurationTracker
{
    private readonly Dictionary<string, (DateTimeOffset StartedOn, string Label)> _started =
        new(StringComparer.Ordinal);

    private readonly Queue<string> _order = new();
    private readonly Lock _gate = new();
    private readonly int _capacity;

    /// <summary>Initializes a new instance of the <see cref="EventDurationTracker"/> class.</summary>
    /// <param name="capacity">How many unfinished items may be tracked at once.</param>
    public EventDurationTracker(int capacity = 10_000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    /// <summary>Gets how many unfinished items are being tracked.</summary>
    public int Tracking
    {
        get
        {
            lock (_gate)
            {
                return _started.Count;
            }
        }
    }

    /// <summary>
    /// Records that something started, remembering the label its measurements should be filed under. The label
    /// matters because several engines announce a start with its definition key and announce the end without
    /// it — without carrying it here, every completion would land in an unlabelled series.
    /// </summary>
    /// <param name="key">What started.</param>
    /// <param name="startedOnUtc">When it started.</param>
    /// <param name="label">The definition key its measurements belong to.</param>
    public void Start(string key, DateTimeOffset startedOnUtc, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        lock (_gate)
        {
            if (_started.TryAdd(key, (startedOnUtc, label)))
            {
                _order.Enqueue(key);
            }
            else
            {
                // A repeated start is the newer truth; the position in the eviction order is already held.
                _started[key] = (startedOnUtc, label);
            }

            while (_started.Count > _capacity && _order.TryDequeue(out var oldest))
            {
                _started.Remove(oldest);
            }
        }
    }

    /// <summary>Records that something finished and reports how long it took.</summary>
    /// <param name="key">What finished.</param>
    /// <param name="finishedOnUtc">When it finished.</param>
    /// <returns>
    /// How long it took and what it was, or <see langword="null"/> when its start was never seen — which
    /// happens legitimately for work that began before this process did.
    /// </returns>
    public (TimeSpan Elapsed, string Label)? Stop(string key, DateTimeOffset finishedOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            if (!_started.Remove(key, out var start))
            {
                return null;
            }

            var elapsed = finishedOnUtc - start.StartedOn;
            return (elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed, start.Label);
        }
    }
}

/// <summary>
/// The shared shape of every bridge from an engine's event stream to the metric store.
/// <para>
/// A bridge does two things in a fixed order. It forwards the event to whatever consumer was already on the
/// seam — so adding monitoring never displaces notifications or audit — and then records what the event
/// measures. If recording fails, the failure is contained and counted rather than thrown: the acceptance rule
/// for this layer is that no engine is affected by being observed, and an exception thrown back into a
/// workflow transition would break exactly that.
/// </para>
/// </summary>
/// <typeparam name="TEvent">The engine event type this bridge consumes.</typeparam>
public abstract class MetricsBridge<TEvent>
    where TEvent : class
{
    private readonly MonitoringMetrics _diagnostics;

    /// <summary>Initializes a new instance of the <see cref="MetricsBridge{TEvent}"/> class.</summary>
    /// <param name="monitoring">The monitoring engine measurements are recorded into.</param>
    /// <param name="diagnostics">The engine's own counters.</param>
    protected MetricsBridge(MonitoringEngine monitoring, MonitoringMetrics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(monitoring);
        ArgumentNullException.ThrowIfNull(diagnostics);
        Monitoring = monitoring;
        _diagnostics = diagnostics;
    }

    /// <summary>Gets the monitoring engine measurements are recorded into.</summary>
    protected MonitoringEngine Monitoring { get; }

    /// <summary>Gets the tracker used to pair start and end events into durations.</summary>
    protected EventDurationTracker Durations { get; } = new();

    /// <summary>Records what an event measures.</summary>
    /// <param name="engineEvent">The event.</param>
    protected abstract void Measure(TEvent engineEvent);

    /// <summary>Records what an event measures, containing and counting any failure.</summary>
    /// <param name="engineEvent">The event.</param>
    protected void MeasureSafely(TEvent engineEvent)
    {
        ArgumentNullException.ThrowIfNull(engineEvent);
        try
        {
            Measure(engineEvent);
        }
#pragma warning disable CA1031 // Being observed must never be able to fail the engine being observed.
        catch (Exception)
#pragma warning restore CA1031
        {
            _diagnostics.RecordBridgeFault();
        }
    }

    /// <summary>Builds the dimension carrying the definition or entity key a measurement belongs to.</summary>
    /// <param name="key">The definition or entity key.</param>
    /// <returns>The dimension.</returns>
    protected static MetricDimension ByKey(string key) =>
        MetricDimension.Of(MetricLabel.Of(Configuration.MonitoringConstants.KeyLabel, key));

    /// <summary>Builds the dimension carrying a key and the outcome of what it measured.</summary>
    /// <param name="key">The definition or entity key.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The dimension.</returns>
    protected static MetricDimension ByOutcome(string key, string outcome) => MetricDimension.Of(
        MetricLabel.Of(Configuration.MonitoringConstants.KeyLabel, key),
        MetricLabel.Of(Configuration.MonitoringConstants.OutcomeLabel, outcome));
}
