using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;

namespace FactoryOS.Plugins.Workflow.Monitoring.Events;

/// <summary>The base of a monitoring event raised by the runtime and published onto the event bus.</summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OccurredOnUtc">When the event occurred.</param>
public abstract record MonitoringEvent(string Tenant, DateTimeOffset OccurredOnUtc);

/// <summary>
/// Raised when a measurement was admitted into a series. Published only when the engine is configured to
/// publish collection events — see <c>MonitoringEngineOptions.PublishCollectionEvents</c>.
/// </summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Instance">The series the measurement joined.</param>
/// <param name="Value">The measurement.</param>
public sealed record MetricCollected(
    string Tenant, DateTimeOffset OccurredOnUtc, MetricInstance Instance, MetricValue Value)
    : MonitoringEvent(Tenant, OccurredOnUtc);

/// <summary>Raised when a series was collapsed into a snapshot over a window.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Snapshot">The snapshot produced.</param>
public sealed record MetricAggregated(string Tenant, DateTimeOffset OccurredOnUtc, MetricSnapshot Snapshot)
    : MonitoringEvent(Tenant, OccurredOnUtc);

/// <summary>Raised when an evaluated threshold put a metric outside its limits.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="ThresholdKey">The threshold that was crossed.</param>
/// <param name="Instance">The series that crossed it.</param>
/// <param name="State">How far outside the limits the metric is.</param>
/// <param name="Value">The aggregated value that crossed.</param>
/// <param name="Limit">The limit it crossed.</param>
/// <param name="Correlation">The operation behind the most recent measurement in the window.</param>
public sealed record ThresholdExceeded(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string ThresholdKey,
    MetricInstance Instance,
    MetricHealthState State,
    double Value,
    double Limit,
    MetricCorrelation Correlation)
    : MonitoringEvent(Tenant, OccurredOnUtc);

/// <summary>Raised every time a health probe runs, whatever it found.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Result">What the probe found.</param>
public sealed record HealthCheckCompleted(string Tenant, DateTimeOffset OccurredOnUtc, HealthCheckResult Result)
    : MonitoringEvent(Tenant, OccurredOnUtc);

/// <summary>
/// Raised only when a component's status actually changed. Kept distinct from
/// <see cref="HealthCheckCompleted"/> on purpose: everything wants to know about a transition, almost nothing
/// wants to know that a healthy component is still healthy.
/// </summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="CheckKey">The component.</param>
/// <param name="Previous">What it was.</param>
/// <param name="Current">What it is now.</param>
/// <param name="Detail">Why it changed.</param>
public sealed record HealthStatusChanged(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string CheckKey,
    HealthStatus Previous,
    HealthStatus Current,
    string Detail)
    : MonitoringEvent(Tenant, OccurredOnUtc);

/// <summary>Raised when measurements outlived their retention and were dropped or rolled up.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Removed">How many raw measurements left the store.</param>
/// <param name="RolledUp">How many aggregated values replaced them.</param>
public sealed record MetricRetentionExpired(
    string Tenant, DateTimeOffset OccurredOnUtc, int Removed, int RolledUp)
    : MonitoringEvent(Tenant, OccurredOnUtc);

/// <summary>Raised when a breach persisted long enough for an alert to open.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Alert">The alert that opened.</param>
public sealed record AlertTriggered(string Tenant, DateTimeOffset OccurredOnUtc, MetricAlert Alert)
    : MonitoringEvent(Tenant, OccurredOnUtc);

/// <summary>Raised when an open alert's metric came back inside its limits.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="AlertKey">The alert that closed.</param>
/// <param name="RuleKey">The rule it belonged to.</param>
/// <param name="Instance">The series it fired for.</param>
/// <param name="OpenFor">How long it was open.</param>
public sealed record AlertResolved(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string AlertKey,
    string RuleKey,
    MetricInstance Instance,
    TimeSpan OpenFor)
    : MonitoringEvent(Tenant, OccurredOnUtc);

/// <summary>
/// Receives monitoring events raised by the runtime — the seam onto the platform event bus. Like the SLA and
/// audit seams this one fans out, so an exporter, a dashboard feed and an alert forwarder can all watch the
/// same stream without displacing each other.
/// </summary>
public interface IMonitoringEventSink
{
    /// <summary>Publishes a monitoring event.</summary>
    /// <param name="monitoringEvent">The event to publish.</param>
    void Publish(MonitoringEvent monitoringEvent);
}

/// <summary>An in-memory <see cref="IMonitoringEventSink"/> that records published events for inspection.</summary>
public sealed class InMemoryMonitoringEventSink : IMonitoringEventSink
{
    private readonly ConcurrentQueue<MonitoringEvent> _events = new();

    /// <summary>Gets the published events in order.</summary>
    public IReadOnlyList<MonitoringEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Publish(MonitoringEvent monitoringEvent)
    {
        ArgumentNullException.ThrowIfNull(monitoringEvent);
        _events.Enqueue(monitoringEvent);
    }
}
