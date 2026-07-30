using System.Collections.Generic;
using System.Linq;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Execution;
using AuditEngine = FactoryOS.Plugins.Workflow.Audit.Execution.AuditEngine;
using MonitoringEngine = FactoryOS.Plugins.Workflow.Monitoring.Execution.MonitoringEngine;

namespace FactoryOS.Api.Platform;

// The read-only projections behind the /platform/* observability endpoints. The logic lives here, as pure
// functions over the engines and the tenant, so it can be tested against the real composed engines without
// standing up the HTTP pipeline. The endpoints in PlatformObservabilityEndpoints are thin wrappers that
// resolve the request's tenant and delegate here. Everything is tenant-scoped: a caller only ever sees its own
// factory's plugins, extensions, audit trail and metrics.

/// <summary>One installed plugin, as seen by an operator.</summary>
internal sealed record PluginView(
    string Key,
    string Version,
    string? PreviousVersion,
    string Status,
    bool Enabled,
    string? FailureReason,
    DateTimeOffset? StartedUtc);

/// <summary>One aspect of a plugin's health.</summary>
internal sealed record HealthProblemView(string Aspect, string Status, string Detail);

/// <summary>A plugin's health: the worst answer, and the aspects that are not healthy.</summary>
internal sealed record HealthView(string Status, IReadOnlyList<HealthProblemView> Problems);

/// <summary>One contribution to a published extension point.</summary>
internal sealed record ExtensionView(
    string PluginKey, string Point, string Name, string? Description, string? Reference);

/// <summary>One audit record, projected for reading.</summary>
internal sealed record AuditView(
    long Sequence,
    string Category,
    string Action,
    string Severity,
    string Result,
    string Actor,
    string Message,
    DateTimeOffset OccurredOnUtc);

/// <summary>The tenant's audit trail plus the chain verification verdict.</summary>
internal sealed record AuditReportView(bool ChainValid, int Verified, IReadOnlyList<AuditView> Records);

/// <summary>One registered metric series.</summary>
internal sealed record MetricView(string Key, string Category, string Kind, string Unit, string Description);

/// <summary>The monitoring engine's own counters plus the registered metric definitions.</summary>
internal sealed record MetricsReportView(
    long Collected,
    long Sampled,
    long Aggregations,
    long ThresholdBreaches,
    long AlertsTriggered,
    long AlertsResolved,
    long HealthChecks,
    long Expired,
    long BridgeFaults,
    IReadOnlyList<MetricView> Definitions);

/// <summary>Pure, tenant-scoped read projections over the platform engines.</summary>
internal static class PlatformObservability
{
    /// <summary>The most recent audit records returned by the trail endpoint.</summary>
    internal const int AuditPageSize = 100;

    /// <summary>Lists a tenant's installed plugins.</summary>
    public static IReadOnlyList<PluginView> Plugins(IPluginRuntime runtime, string tenant)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return [.. runtime.Installed(tenant).Select(ToView)];
    }

    /// <summary>Reports a plugin's health for a tenant.</summary>
    public static HealthView Health(IPluginRuntime runtime, string tenant, string pluginKey)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var report = runtime.Health(tenant, pluginKey);
        return new HealthView(
            report.Status.ToString(),
            [.. report.Problems.Select(problem => new HealthProblemView(
                problem.Aspect.ToString(), problem.Status.ToString(), problem.Detail))]);
    }

    /// <summary>Lists what currently extends a published extension point, for a tenant.</summary>
    public static IReadOnlyList<ExtensionView> Extensions(
        IPluginRuntime runtime, string tenant, PluginExtensionPointKind kind)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return [.. runtime.Extensions(tenant, kind).Select(extension => new ExtensionView(
            extension.PluginKey,
            extension.Contribution.Point.Key,
            extension.Contribution.Name,
            extension.Contribution.Description,
            extension.Contribution.Reference))];
    }

    /// <summary>Returns a tenant's most recent audit records, newest first, and the chain verdict.</summary>
    public static AuditReportView Audit(AuditEngine audit, string tenant)
    {
        ArgumentNullException.ThrowIfNull(audit);

        var records = audit.ListByTenant(tenant)
            .OrderByDescending(record => record.Sequence)
            .Take(AuditPageSize)
            .Select(record => new AuditView(
                record.Sequence,
                record.Category.ToString(),
                record.Action.ToString(),
                record.Severity.ToString(),
                record.Result.ToString(),
                record.Actor.DisplayName ?? record.Actor.Id,
                record.Message,
                record.OccurredOnUtc))
            .ToArray();

        var verification = audit.Verify(tenant);
        return new AuditReportView(verification.IsValid, verification.Verified, records);
    }

    /// <summary>Returns the monitoring engine's counters and the registered metric definitions.</summary>
    public static MetricsReportView Metrics(MonitoringEngine monitoring)
    {
        ArgumentNullException.ThrowIfNull(monitoring);

        var snapshot = monitoring.Snapshot();
        var definitions = monitoring.Definitions()
            .Select(definition => new MetricView(
                definition.Key,
                definition.Category.ToString(),
                definition.Kind.ToString(),
                definition.Unit,
                definition.Description))
            .ToArray();

        return new MetricsReportView(
            snapshot.Collected,
            snapshot.Sampled,
            snapshot.Aggregations,
            snapshot.ThresholdBreaches,
            snapshot.AlertsTriggered,
            snapshot.AlertsResolved,
            snapshot.HealthChecks,
            snapshot.Expired,
            snapshot.BridgeFaults,
            definitions);
    }

    private static PluginView ToView(PluginInstance instance) => new(
        instance.PluginKey,
        instance.Version.ToString(),
        instance.PreviousVersion?.ToString(),
        instance.Status.ToString(),
        instance.Enabled,
        instance.FailureReason,
        instance.StartedUtc);
}
