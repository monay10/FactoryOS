using System.Globalization;
using FactoryOS.Plugins.Workflow.Monitoring.Collections;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;

namespace FactoryOS.Plugins.Workflow.Monitoring.Health;

/// <summary>
/// What a component's health is judged on: how much of its work failed, and whether it produced any signal at
/// all.
/// </summary>
/// <param name="SuccessMetric">The metric counting what the component got right.</param>
/// <param name="FailureMetric">The metric counting what went wrong.</param>
/// <param name="DegradedAbove">The failure ratio above which the component is degraded.</param>
/// <param name="UnhealthyAbove">The failure ratio above which the component is failing.</param>
public sealed record HealthSignal(
    string SuccessMetric, string FailureMetric, double DegradedAbove = 0.05, double UnhealthyAbove = 0.25);

/// <summary>
/// A health probe that reads what a component actually did rather than asking it how it feels.
/// <para>
/// This is the shape every check in the platform takes, and it is a deliberate choice. A probe that called into
/// the workflow runtime to ask after its health would put an arrow from monitoring back to the engine it
/// observes, which is precisely the dependency this layer exists to avoid — and it would answer "healthy" for a
/// runtime that is up but failing every instance it starts. Reading the measurements the engine's own events
/// produced avoids both problems: the arrow keeps pointing one way, and the answer is about outcomes.
/// </para>
/// <para>
/// Silence is reported as <see cref="HealthStatus.Unknown"/>, never as health. A queue nobody used this hour is
/// not broken, but it is also not evidence that anything works — and a report that rounds "no signal" up to
/// "healthy" is a report that says everything is fine during an outage.
/// </para>
/// </summary>
public sealed class MetricHealthCheck
{
    private readonly HealthSignal _signal;
    private readonly string _componentName;

    /// <summary>Initializes a new instance of the <see cref="MetricHealthCheck"/> class.</summary>
    /// <param name="componentName">The component's display name, used in the detail text.</param>
    /// <param name="signal">What the component is judged on.</param>
    public MetricHealthCheck(string componentName, HealthSignal signal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);
        ArgumentNullException.ThrowIfNull(signal);
        _componentName = componentName;
        _signal = signal;
    }

    /// <summary>Answers for the component.</summary>
    /// <param name="context">What the probe has to work with.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>What the probe found.</returns>
    public ValueTask<HealthCheckResult> ProbeAsync(
        HealthProbeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var key = KeyOf(_componentName);
        var successes = context.Total(_signal.SuccessMetric);
        var failures = context.Total(_signal.FailureMetric);

        if (successes == 0 && failures == 0)
        {
            return ValueTask.FromResult(HealthCheckResult.Unknown(
                key, context.NowUtc, $"{_componentName} produced no signal in the last {context.Window}."));
        }

        // The two metrics count disjoint outcomes, so what was attempted is their sum — which is also why a
        // component whose only signal is failure lands on a ratio of one rather than on a division by zero.
        var attempted = successes + failures;
        var ratio = failures / attempted;
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["attempted"] = attempted.ToString("0.##", CultureInfo.InvariantCulture),
            ["failed"] = failures.ToString("0.##", CultureInfo.InvariantCulture),
            ["failureRatio"] = ratio.ToString("0.###", CultureInfo.InvariantCulture),
            ["window"] = context.Window.ToString(),
        };

        var percent = (ratio * 100).ToString("0.#", CultureInfo.InvariantCulture);
        var result = ratio > _signal.UnhealthyAbove
            ? HealthCheckResult.Unhealthy(
                key, context.NowUtc, $"{_componentName} failed {percent}% of its work.", data)
            : ratio > _signal.DegradedAbove
                ? HealthCheckResult.Degraded(
                    key, context.NowUtc, $"{_componentName} failed {percent}% of its work.", data)
                : HealthCheckResult.Healthy(
                    key, context.NowUtc, $"{_componentName} failed {percent}% of its work.", data);

        return ValueTask.FromResult(result);
    }

    /// <summary>Builds the stable check key for a component name.</summary>
    /// <param name="componentName">The component name.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(string componentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);
        return componentName.Trim().ToLowerInvariant().Replace(' ', '-');
    }
}

/// <summary>
/// The twelve components the platform reports on: the seven engines, plus connectors, plugins, the database,
/// object storage and configuration.
/// <para>
/// Each is judged the same way — what it attempted against what failed — because that is the only question with
/// the same meaning across all of them, and because a set of checks that each measured something different
/// would produce a report nobody could read as a whole.
/// </para>
/// </summary>
public static class PlatformHealthChecks
{
    /// <summary>Gets the checks and the probes that answer for them.</summary>
    /// <returns>The registrations, in the order they are reported.</returns>
    public static IReadOnlyList<(HealthCheck Check, HealthProbe Probe)> All() =>
    [
        Build("Workflow Engine", MetricCategory.Workflow, true, new HealthSignal(
            WorkflowMetricCollection.InstancesCompleted, WorkflowMetricCollection.InstancesFailed)),
        Build("Forms Engine", MetricCategory.Form, false, new HealthSignal(
            FormsMetricCollection.InstancesSubmitted, FormsMetricCollection.InstancesRejected,
            DegradedAbove: 0.30, UnhealthyAbove: 0.60)),
        Build("Human Task Engine", MetricCategory.HumanTask, false, new HealthSignal(
            HumanTaskMetricCollection.Completed, HumanTaskMetricCollection.Expired,
            DegradedAbove: 0.10, UnhealthyAbove: 0.30)),
        Build("Approval Engine", MetricCategory.Approval, false, new HealthSignal(
            ApprovalMetricCollection.Completed, ApprovalMetricCollection.Expired,
            DegradedAbove: 0.10, UnhealthyAbove: 0.30)),
        Build("Notification Engine", MetricCategory.Notification, true, new HealthSignal(
            NotificationMetricCollection.Sent, NotificationMetricCollection.DeadLettered)),
        Build("SLA Engine", MetricCategory.Sla, false, new HealthSignal(
            SlaMetricCollection.Completed, SlaMetricCollection.TimedOut,
            DegradedAbove: 0.10, UnhealthyAbove: 0.30)),
        // Judged on tamper detections, not on retention: records leaving the trail because their policy said
        // so is the system working, and a single record that fails verification is the system failing.
        Build("Audit Engine", MetricCategory.Audit, true, new HealthSignal(
            AuditMetricCollection.Recorded, AuditMetricCollection.TamperDetections,
            DegradedAbove: 0.0, UnhealthyAbove: 0.0)),
        Build("Connectors", MetricCategory.Connector, true, new HealthSignal(
            ConnectorMetricCollection.Calls, ConnectorMetricCollection.Failures)),
        Build("Plugins", MetricCategory.Plugin, true, new HealthSignal(
            PluginMetricCollection.Loaded, PluginMetricCollection.Failures,
            DegradedAbove: 0.0, UnhealthyAbove: 0.10)),
        Build("Database", MetricCategory.Infrastructure, true, new HealthSignal(
            InfrastructureMetricCollection.DatabaseQueries, InfrastructureMetricCollection.DatabaseErrors,
            DegradedAbove: 0.01, UnhealthyAbove: 0.10)),
        Build("Storage", MetricCategory.Infrastructure, false, new HealthSignal(
            InfrastructureMetricCollection.StorageOperations, InfrastructureMetricCollection.StorageErrors,
            DegradedAbove: 0.01, UnhealthyAbove: 0.10)),
        // Configuration is judged strictly: a single rejected section is a factory running on settings
        // somebody believes were applied, which is a worse failure than a slow query.
        Build("Configuration", MetricCategory.Infrastructure, true, new HealthSignal(
            InfrastructureMetricCollection.ConfigurationApplied,
            InfrastructureMetricCollection.ConfigurationErrors,
            DegradedAbove: 0.0, UnhealthyAbove: 0.05)),
    ];

    private static (HealthCheck, HealthProbe) Build(
        string name, MetricCategory category, bool critical, HealthSignal signal)
    {
        var probe = new MetricHealthCheck(name, signal);
        var check = new HealthCheck(MetricHealthCheck.KeyOf(name), name, category)
        {
            IsCritical = critical,
            Description = $"Judges {name} on {signal.FailureMetric} against {signal.SuccessMetric}.",
        };

        return (check, probe.ProbeAsync);
    }
}
