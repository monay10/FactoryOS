using System.Collections.Concurrent;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Events;
using FactoryOS.Plugins.Runtime.Integration;
using Microsoft.Extensions.Options;

namespace FactoryOS.Plugins.Runtime.Execution;

/// <summary>
/// The one place a lifecycle transition is told to the outside world: as an event, as an audit line and as a
/// measurement.
/// <para>
/// Gathering the three here is what keeps them consistent. All of them are derived from the same
/// <see cref="PluginTelemetry"/>, so the event history, the audit trail and the dashboard cannot end up
/// telling three versions of what happened.
/// </para>
/// </summary>
public sealed class PluginRuntimeAnnouncer
{
    private readonly ConcurrentDictionary<string, PluginMetrics> _metrics =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly PluginRuntimePublisher _events;
    private readonly PluginAuditPublisher _audit;
    private readonly PluginMetricPublisher _measurements;
    private readonly PluginRuntimeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="PluginRuntimeAnnouncer"/> class.</summary>
    /// <param name="events">The runtime event publisher.</param>
    /// <param name="audit">The audit publisher.</param>
    /// <param name="measurements">The metric publisher.</param>
    /// <param name="options">The runtime options.</param>
    public PluginRuntimeAnnouncer(
        PluginRuntimePublisher events,
        PluginAuditPublisher audit,
        PluginMetricPublisher measurements,
        IOptions<PluginRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(measurements);
        ArgumentNullException.ThrowIfNull(options);

        _events = events;
        _audit = audit;
        _measurements = measurements;
        _options = options.Value;
    }

    /// <summary>Publishes one runtime event, when the host has asked for lifecycle events at all.</summary>
    /// <param name="runtimeEvent">The event.</param>
    public void Publish(PluginRuntimeEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        if (_options.PublishLifecycleEvents)
        {
            _events.Publish(runtimeEvent);
        }
    }

    /// <summary>
    /// Records one transition: always as a measurement, and as an audit line when it failed or when the host
    /// audits successes too.
    /// <para>
    /// A <b>failure is always audited</b>, whatever the configuration says. Turning off audit noise is a
    /// legitimate thing for an operator to want; losing the record of the install that did not happen is not,
    /// and those are the lines someone comes looking for months later.
    /// </para>
    /// </summary>
    /// <param name="telemetry">What happened.</param>
    public void Announce(PluginTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        _measurements.Record(telemetry);
        Metrics(telemetry.Tenant, telemetry.PluginKey).Record(telemetry);

        if (!telemetry.Succeeded || _options.AuditLifecycle)
        {
            _audit.Write(PluginAuditEntry.From(telemetry));
        }
    }

    /// <summary>Records that the sandbox refused an action.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin.</param>
    /// <param name="refusal">Why it was refused.</param>
    /// <param name="occurredUtc">When it happened.</param>
    public void Refused(
        string tenant, string pluginKey, Isolation.PluginSandboxRefusal refusal, DateTimeOffset occurredUtc) =>
        _measurements.RecordRefusal(tenant, pluginKey, refusal, occurredUtc);

    /// <summary>Gets one instance's running tally.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin.</param>
    /// <returns>The tally.</returns>
    public PluginMetrics Metrics(string tenant, string pluginKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        return _metrics.GetOrAdd(
            PluginInstance.Identify(tenant, pluginKey), _ => new PluginMetrics(tenant, pluginKey));
    }

    /// <summary>Takes a snapshot of every instance's tally.</summary>
    /// <returns>The snapshots.</returns>
    public IReadOnlyList<PluginMetricsSnapshot> Snapshots() =>
        [.. _metrics.Values.Select(metrics => metrics.Snapshot())
            .OrderBy(snapshot => snapshot.Tenant, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.PluginKey, StringComparer.Ordinal)];
}
