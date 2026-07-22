using System.Collections.Concurrent;
using FactoryOS.Connectors.Runtime.Domain;

namespace FactoryOS.Connectors.Runtime.Integration;

/// <summary>
/// One line of the connector runtime's audit trail: who invoked what, against which external system, and how
/// it ended.
/// <para>
/// The runtime does not write into the platform's audit engine — it cannot even see it, and that is the
/// point. It states what happened in its own vocabulary and a host adapter maps it onto whichever trail the
/// deployment keeps.
/// </para>
/// </summary>
/// <param name="Tenant">The tenant the invocation was made in.</param>
/// <param name="Subject">Who invoked it, or <c>anonymous</c> when the request named nobody.</param>
/// <param name="Instance">The instance key.</param>
/// <param name="Definition">The definition key.</param>
/// <param name="Operation">The operation name.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="OccurredUtc">When it ended.</param>
public sealed record ConnectorAuditEntry(
    string Tenant,
    string Subject,
    string Instance,
    string Definition,
    string Operation,
    string Outcome,
    DateTimeOffset OccurredUtc)
{
    /// <summary>Gets whether the invocation succeeded.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Gets why it failed, when it did.</summary>
    public ConnectorError? Error { get; init; }

    /// <summary>Gets how long it took.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets how many attempts were made.</summary>
    public int Attempts { get; init; } = 1;

    /// <summary>Gets the identifiers tying the line back to the work that caused it.</summary>
    public ConnectorCorrelation Correlation { get; init; } = ConnectorCorrelation.Empty;

    /// <summary>Builds an audit line from an invocation's telemetry.</summary>
    /// <param name="telemetry">The telemetry.</param>
    /// <param name="subject">Who invoked it.</param>
    /// <returns>The audit line.</returns>
    public static ConnectorAuditEntry From(ConnectorTelemetry telemetry, string subject)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return new ConnectorAuditEntry(
            telemetry.Tenant,
            subject,
            telemetry.Instance,
            telemetry.Definition,
            telemetry.Operation,
            telemetry.Outcome,
            telemetry.StartedUtc + telemetry.Duration)
        {
            Succeeded = telemetry.Succeeded,
            Error = telemetry.Error,
            Duration = telemetry.Duration,
            Attempts = telemetry.Attempts,
            Correlation = telemetry.Correlation,
        };
    }
}

/// <summary>Receives the connector runtime's audit lines. Every registered sink is notified.</summary>
public interface IConnectorAuditSink
{
    /// <summary>Records an audit line.</summary>
    /// <param name="entry">The line.</param>
    void Record(ConnectorAuditEntry entry);
}

/// <summary>A bounded in-memory <see cref="IConnectorAuditSink"/> — the default, so a container is useful alone.</summary>
public sealed class InMemoryConnectorAuditSink : IConnectorAuditSink
{
    /// <summary>How many lines are retained before the oldest are dropped.</summary>
    public const int HistoryLimit = 1000;

    private readonly ConcurrentQueue<ConnectorAuditEntry> _entries = new();

    /// <inheritdoc />
    public void Record(ConnectorAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _entries.Enqueue(entry);
        while (_entries.Count > HistoryLimit && _entries.TryDequeue(out _))
        {
            // Drop the oldest until the history is back inside its bound.
        }
    }

    /// <summary>Gets every retained line, oldest first.</summary>
    /// <returns>The lines.</returns>
    public IReadOnlyList<ConnectorAuditEntry> All() => [.. _entries];

    /// <summary>Gets one tenant's retained lines, oldest first.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The lines.</returns>
    public IReadOnlyList<ConnectorAuditEntry> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return [.. _entries.Where(entry => string.Equals(entry.Tenant, tenant, StringComparison.OrdinalIgnoreCase))];
    }
}

/// <summary>
/// One number the connector runtime measured, with the labels that slice it. Kept deliberately shapeless —
/// a name, a value and labels — so a host can map it onto whichever metrics system it runs without the
/// runtime knowing that system exists.
/// </summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="Name">The metric name.</param>
/// <param name="Value">The measured number.</param>
/// <param name="Labels">The labels slicing the series.</param>
/// <param name="ObservedUtc">When it was measured.</param>
public sealed record ConnectorMeasurement(
    string Tenant,
    string Name,
    double Value,
    IReadOnlyDictionary<string, string> Labels,
    DateTimeOffset ObservedUtc)
{
    /// <summary>Gets the identifiers tying the measurement back to the work that produced it.</summary>
    public ConnectorCorrelation Correlation { get; init; } = ConnectorCorrelation.Empty;
}

/// <summary>Receives the numbers the connector runtime measures. Every registered sink is notified.</summary>
public interface IConnectorMetricSink
{
    /// <summary>Records a measurement.</summary>
    /// <param name="measurement">The measurement.</param>
    void Observe(ConnectorMeasurement measurement);
}

/// <summary>
/// A bounded in-memory <see cref="IConnectorMetricSink"/> that also keeps running totals per metric name, so
/// a deployment without a metrics backend can still answer "how many ERP calls failed this shift?".
/// </summary>
public sealed class InMemoryConnectorMetricSink : IConnectorMetricSink
{
    /// <summary>How many measurements are retained before the oldest are dropped.</summary>
    public const int HistoryLimit = 5000;

    private readonly ConcurrentQueue<ConnectorMeasurement> _measurements = new();
    private readonly ConcurrentDictionary<string, double> _totals = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Observe(ConnectorMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        _measurements.Enqueue(measurement);
        _totals.AddOrUpdate(measurement.Name, measurement.Value, (_, current) => current + measurement.Value);

        while (_measurements.Count > HistoryLimit && _measurements.TryDequeue(out _))
        {
            // Drop the oldest until the history is back inside its bound.
        }
    }

    /// <summary>Gets every retained measurement, oldest first.</summary>
    /// <returns>The measurements.</returns>
    public IReadOnlyList<ConnectorMeasurement> All() => [.. _measurements];

    /// <summary>Gets the retained measurements of one metric, oldest first.</summary>
    /// <param name="name">The metric name.</param>
    /// <returns>The measurements.</returns>
    public IReadOnlyList<ConnectorMeasurement> For(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return [.. _measurements.Where(m => string.Equals(m.Name, name, StringComparison.Ordinal))];
    }

    /// <summary>Gets the running total of one metric.</summary>
    /// <param name="name">The metric name.</param>
    /// <returns>The total, or zero when nothing was measured.</returns>
    public double Total(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _totals.TryGetValue(name, out var total) ? total : 0;
    }
}

/// <summary>The metric names the connector runtime reports.</summary>
public static class ConnectorMetricNames
{
    /// <summary>Every invocation, sliced by connector, operation and outcome.</summary>
    public const string Invocations = "connector.invocations";

    /// <summary>How long an invocation took, in milliseconds.</summary>
    public const string Duration = "connector.duration.ms";

    /// <summary>Attempts beyond the first.</summary>
    public const string Retries = "connector.retries";

    /// <summary>Answers served from the cache.</summary>
    public const string CacheHits = "connector.cache.hits";

    /// <summary>Invocations refused by a rate limit.</summary>
    public const string Throttled = "connector.throttled";

    /// <summary>Invocations refused because a circuit was open.</summary>
    public const string CircuitRefusals = "connector.circuit.refusals";

    /// <summary>Invocations refused on authentication or authorization.</summary>
    public const string Refusals = "connector.refusals";

    /// <summary>Invocations that failed against the external system.</summary>
    public const string Failures = "connector.failures";
}

/// <summary>Publishes audit lines to every registered sink, so one subscriber cannot silence the others.</summary>
public sealed class ConnectorAuditPublisher
{
    private readonly IReadOnlyList<IConnectorAuditSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="ConnectorAuditPublisher"/> class.</summary>
    /// <param name="sinks">The sinks.</param>
    public ConnectorAuditPublisher(IEnumerable<IConnectorAuditSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <summary>Records a line in every sink.</summary>
    /// <param name="entry">The line.</param>
    public void Record(ConnectorAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        foreach (var sink in _sinks)
        {
            sink.Record(entry);
        }
    }
}

/// <summary>Publishes measurements to every registered sink.</summary>
public sealed class ConnectorMetricPublisher
{
    private readonly IReadOnlyList<IConnectorMetricSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="ConnectorMetricPublisher"/> class.</summary>
    /// <param name="sinks">The sinks.</param>
    public ConnectorMetricPublisher(IEnumerable<IConnectorMetricSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <summary>Records a measurement in every sink.</summary>
    /// <param name="measurement">The measurement.</param>
    public void Observe(ConnectorMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        foreach (var sink in _sinks)
        {
            sink.Observe(measurement);
        }
    }
}
