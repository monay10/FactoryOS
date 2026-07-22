using System.Globalization;
using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Plugins.Runtime.Domain;

/// <summary>
/// What one lifecycle transition cost and how it ended. This is the single record the audit sink, the metric
/// sink and the runtime event are all derived from, so the three can never disagree about what happened.
/// </summary>
/// <param name="Tenant">The tenant the transition ran for.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="Version">The version involved.</param>
/// <param name="Phase">Which lifecycle step ran.</param>
/// <param name="Succeeded">Whether it succeeded.</param>
/// <param name="Duration">How long it took.</param>
/// <param name="OccurredUtc">When it finished.</param>
public sealed record PluginTelemetry(
    string Tenant,
    string PluginKey,
    PluginVersion Version,
    PluginLifecyclePhase Phase,
    bool Succeeded,
    TimeSpan Duration,
    DateTimeOffset OccurredUtc)
{
    /// <summary>Gets why the transition failed, when it did.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Gets how the transition failed, when it did.</summary>
    public PluginFailureKind FailureKind { get; init; }

    /// <summary>Gets who asked for the transition.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the outcome as the single word measurements and audit lines label it with.</summary>
    public string Outcome => Succeeded ? "success" : "failure";

    /// <inheritdoc />
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture, $"{Tenant}|{PluginKey}@{Version} {Phase} {Outcome}");
}

/// <summary>A point-in-time tally of what a plugin instance's lifecycle has done.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="Transitions">How many transitions ran.</param>
/// <param name="Failures">How many of them failed.</param>
/// <param name="Starts">How many times it started.</param>
/// <param name="Updates">How many times it was updated.</param>
/// <param name="Rollbacks">How many times an update was rolled back.</param>
/// <param name="LastTransitionUtc">When the last transition ran.</param>
public sealed record PluginMetricsSnapshot(
    string Tenant,
    string PluginKey,
    long Transitions,
    long Failures,
    long Starts,
    long Updates,
    long Rollbacks,
    DateTimeOffset? LastTransitionUtc)
{
    /// <summary>
    /// Gets the share of transitions that failed, or <see langword="null"/> when nothing has run.
    /// <para>
    /// It is deliberately null rather than zero before the first transition: "nothing has happened" and
    /// "everything has succeeded" are different facts, and a dashboard that conflates them shows a green
    /// plugin that has never actually started.
    /// </para>
    /// </summary>
    public double? FailureRate => Transitions == 0 ? null : (double)Failures / Transitions;
}

/// <summary>A lock-guarded tally of one plugin instance's lifecycle transitions.</summary>
public sealed class PluginMetrics
{
    private readonly Lock _gate = new();
    private long _transitions;
    private long _failures;
    private long _starts;
    private long _updates;
    private long _rollbacks;
    private DateTimeOffset? _last;

    /// <summary>Initializes a new instance of the <see cref="PluginMetrics"/> class.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin.</param>
    public PluginMetrics(string tenant, string pluginKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        Tenant = tenant;
        PluginKey = pluginKey;
    }

    /// <summary>Gets the tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets the plugin.</summary>
    public string PluginKey { get; }

    /// <summary>Records one transition.</summary>
    /// <param name="telemetry">What happened.</param>
    public void Record(PluginTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        lock (_gate)
        {
            _transitions++;
            _last = telemetry.OccurredUtc;

            if (!telemetry.Succeeded)
            {
                _failures++;
                return;
            }

            switch (telemetry.Phase)
            {
                case PluginLifecyclePhase.Start:
                    _starts++;
                    break;
                case PluginLifecyclePhase.Update:
                    _updates++;
                    break;
                case PluginLifecyclePhase.Rollback:
                    _rollbacks++;
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Takes a snapshot of the tally.</summary>
    /// <returns>The snapshot.</returns>
    public PluginMetricsSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new PluginMetricsSnapshot(
                Tenant, PluginKey, _transitions, _failures, _starts, _updates, _rollbacks, _last);
        }
    }
}
