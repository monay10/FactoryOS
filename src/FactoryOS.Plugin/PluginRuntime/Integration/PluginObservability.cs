using System.Globalization;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Integration;

/// <summary>
/// One line in the plugin trail: who changed which tenant's plugin, how, and whether it worked.
/// <para>
/// The runtime states this in <b>its own</b> vocabulary and a host adapter maps it onto the platform's audit
/// engine. That is what lets the runtime be built, tested and shipped without depending on an engine it must
/// never reference.
/// </para>
/// </summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="Version">The version involved.</param>
/// <param name="Phase">Which lifecycle step ran.</param>
/// <param name="Succeeded">Whether it succeeded.</param>
/// <param name="OccurredUtc">When it happened.</param>
public sealed record PluginAuditEntry(
    string Tenant,
    string PluginKey,
    Contracts.Plugins.PluginVersion Version,
    PluginLifecyclePhase Phase,
    bool Succeeded,
    DateTimeOffset OccurredUtc)
{
    /// <summary>Gets who asked for the transition.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets why it failed, when it did.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Builds an audit line from one transition's telemetry.</summary>
    /// <param name="telemetry">What happened.</param>
    /// <returns>The audit line.</returns>
    public static PluginAuditEntry From(PluginTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        return new PluginAuditEntry(
            telemetry.Tenant,
            telemetry.PluginKey,
            telemetry.Version,
            telemetry.Phase,
            telemetry.Succeeded,
            telemetry.OccurredUtc)
        {
            Subject = telemetry.Subject,
            FailureReason = telemetry.FailureReason,
        };
    }

    /// <inheritdoc />
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{OccurredUtc:O} {Tenant}|{PluginKey}@{Version} {Phase} {(Succeeded ? "success" : "failure")}");
}

/// <summary>Where the runtime's audit lines go. The default keeps a bounded in-memory trail.</summary>
public interface IPluginAuditSink
{
    /// <summary>Receives one audit line.</summary>
    /// <param name="entry">The line.</param>
    void Write(PluginAuditEntry entry);
}

/// <summary>A bounded in-memory <see cref="IPluginAuditSink"/>, the default when a host supplies none.</summary>
public sealed class InMemoryPluginAuditSink : IPluginAuditSink
{
    /// <summary>The number of entries retained before the oldest are dropped.</summary>
    public const int TrailLimit = 1000;

    private readonly Lock _gate = new();
    private readonly Queue<PluginAuditEntry> _entries = new();

    /// <inheritdoc />
    public void Write(PluginAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > TrailLimit)
            {
                _entries.Dequeue();
            }
        }
    }

    /// <summary>Gets every retained entry, oldest first.</summary>
    /// <returns>The entries.</returns>
    public IReadOnlyList<PluginAuditEntry> All()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }

    /// <summary>Gets the retained entries concerning one tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The entries, oldest first.</returns>
    public IReadOnlyList<PluginAuditEntry> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        lock (_gate)
        {
            return [.. _entries.Where(entry =>
                string.Equals(entry.Tenant, tenant, StringComparison.OrdinalIgnoreCase))];
        }
    }
}

/// <summary>Fans one audit line out to every registered sink.</summary>
public sealed class PluginAuditPublisher
{
    private readonly IReadOnlyList<IPluginAuditSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="PluginAuditPublisher"/> class.</summary>
    /// <param name="sinks">Every registered sink.</param>
    public PluginAuditPublisher(IEnumerable<IPluginAuditSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <summary>Gets the number of sinks the publisher fans out to.</summary>
    public int SinkCount => _sinks.Count;

    /// <summary>Writes one line to every sink.</summary>
    /// <param name="entry">The line.</param>
    public void Write(PluginAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        foreach (var sink in _sinks)
        {
            sink.Write(entry);
        }
    }
}

/// <summary>One measurement the runtime produces.</summary>
/// <param name="Name">What was measured.</param>
/// <param name="Value">The value.</param>
/// <param name="OccurredUtc">When it was taken.</param>
/// <param name="Labels">The dimensions it is filed under.</param>
public sealed record PluginMeasurement(
    string Name, double Value, DateTimeOffset OccurredUtc, IReadOnlyDictionary<string, string> Labels)
{
    /// <inheritdoc />
    public override string ToString()
    {
        var labels = string.Join(',', Labels.Select(pair => $"{pair.Key}={pair.Value}"));
        return string.Create(CultureInfo.InvariantCulture, $"{Name}{{{labels}}} {Value}");
    }
}

/// <summary>The measurements the plugin runtime produces.</summary>
public static class PluginMetricNames
{
    /// <summary>How many lifecycle transitions ran.</summary>
    public const string Transitions = "plugin.transitions";

    /// <summary>How long a lifecycle transition took, in milliseconds.</summary>
    public const string TransitionDuration = "plugin.transition.duration.ms";

    /// <summary>How many lifecycle transitions failed.</summary>
    public const string Failures = "plugin.failures";

    /// <summary>How many packages were installed.</summary>
    public const string Installs = "plugin.installs";

    /// <summary>How many plugins were started.</summary>
    public const string Starts = "plugin.starts";

    /// <summary>How many plugins were stopped.</summary>
    public const string Stops = "plugin.stops";

    /// <summary>How many plugins were updated.</summary>
    public const string Updates = "plugin.updates";

    /// <summary>How many updates were rolled back.</summary>
    public const string Rollbacks = "plugin.rollbacks";

    /// <summary>How many actions the sandbox refused.</summary>
    public const string SandboxRefusals = "plugin.sandbox.refusals";
}

/// <summary>Where the runtime's measurements go. The default keeps a bounded in-memory series.</summary>
public interface IPluginMetricSink
{
    /// <summary>Receives one measurement.</summary>
    /// <param name="measurement">The measurement.</param>
    void Record(PluginMeasurement measurement);
}

/// <summary>A bounded in-memory <see cref="IPluginMetricSink"/>, the default when a host supplies none.</summary>
public sealed class InMemoryPluginMetricSink : IPluginMetricSink
{
    /// <summary>The number of measurements retained before the oldest are dropped.</summary>
    public const int SeriesLimit = 2000;

    private readonly Lock _gate = new();
    private readonly Queue<PluginMeasurement> _measurements = new();

    /// <inheritdoc />
    public void Record(PluginMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        lock (_gate)
        {
            _measurements.Enqueue(measurement);
            while (_measurements.Count > SeriesLimit)
            {
                _measurements.Dequeue();
            }
        }
    }

    /// <summary>Gets every retained measurement, oldest first.</summary>
    /// <returns>The measurements.</returns>
    public IReadOnlyList<PluginMeasurement> All()
    {
        lock (_gate)
        {
            return [.. _measurements];
        }
    }

    /// <summary>Gets the retained measurements of one name.</summary>
    /// <param name="name">The measurement name.</param>
    /// <returns>The measurements, oldest first.</returns>
    public IReadOnlyList<PluginMeasurement> Named(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_gate)
        {
            return [.. _measurements.Where(measurement =>
                string.Equals(measurement.Name, name, StringComparison.Ordinal))];
        }
    }
}

/// <summary>Fans one measurement out to every registered sink, and turns telemetry into measurements.</summary>
public sealed class PluginMetricPublisher
{
    private readonly IReadOnlyList<IPluginMetricSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="PluginMetricPublisher"/> class.</summary>
    /// <param name="sinks">Every registered sink.</param>
    public PluginMetricPublisher(IEnumerable<IPluginMetricSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <summary>Gets the number of sinks the publisher fans out to.</summary>
    public int SinkCount => _sinks.Count;

    /// <summary>Records one measurement to every sink.</summary>
    /// <param name="measurement">The measurement.</param>
    public void Record(PluginMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        foreach (var sink in _sinks)
        {
            sink.Record(measurement);
        }
    }

    /// <summary>Records the measurements one lifecycle transition produces.</summary>
    /// <param name="telemetry">What happened.</param>
    public void Record(PluginTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PluginRuntimeConstants.TenantLabel] = telemetry.Tenant,
            [PluginRuntimeConstants.PluginLabel] = telemetry.PluginKey,
            [PluginRuntimeConstants.PhaseLabel] = telemetry.Phase.ToString(),
            [PluginRuntimeConstants.OutcomeLabel] = telemetry.Outcome,
        };

        Record(new PluginMeasurement(PluginMetricNames.Transitions, 1, telemetry.OccurredUtc, labels));
        Record(new PluginMeasurement(
            PluginMetricNames.TransitionDuration, telemetry.Duration.TotalMilliseconds, telemetry.OccurredUtc, labels));

        if (!telemetry.Succeeded)
        {
            Record(new PluginMeasurement(PluginMetricNames.Failures, 1, telemetry.OccurredUtc, labels));
            return;
        }

        var name = telemetry.Phase switch
        {
            PluginLifecyclePhase.Install => PluginMetricNames.Installs,
            PluginLifecyclePhase.Start => PluginMetricNames.Starts,
            PluginLifecyclePhase.Stop => PluginMetricNames.Stops,
            PluginLifecyclePhase.Update => PluginMetricNames.Updates,
            PluginLifecyclePhase.Rollback => PluginMetricNames.Rollbacks,
            _ => null,
        };

        if (name is not null)
        {
            Record(new PluginMeasurement(name, 1, telemetry.OccurredUtc, labels));
        }
    }

    /// <summary>Records that the sandbox refused an action.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin.</param>
    /// <param name="refusal">Why it was refused.</param>
    /// <param name="occurredUtc">When it happened.</param>
    public void RecordRefusal(
        string tenant, string pluginKey, Isolation.PluginSandboxRefusal refusal, DateTimeOffset occurredUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        Record(new PluginMeasurement(
            PluginMetricNames.SandboxRefusals,
            1,
            occurredUtc,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PluginRuntimeConstants.TenantLabel] = tenant,
                [PluginRuntimeConstants.PluginLabel] = pluginKey,
                ["refusal"] = refusal.ToString(),
            }));
    }
}
