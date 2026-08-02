using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Execution;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using AuditEngine = FactoryOS.Plugins.Workflow.Audit.Execution.AuditEngine;
using AuditQuery = FactoryOS.Plugins.Workflow.Audit.Execution.AuditQuery;
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

/// <summary>The records matching an audit search, newest first, plus how many matched.</summary>
internal sealed record AuditSearchView(int Count, IReadOnlyList<AuditView> Records);

/// <summary>The outcome of parsing search filters: either a query to run, or the reason the filters were rejected.</summary>
internal sealed record AuditQueryParse(AuditQuery? Query, string? Error)
{
    /// <summary>Whether the filters parsed into a runnable query.</summary>
    public bool Ok => Query is not null;
}

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

    /// <summary>The largest page an audit search may return, so a search can never ask for an unbounded result.</summary>
    internal const int MaxAuditSearchLimit = 500;

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
            .Select(ToAuditView)
            .ToArray();

        var verification = audit.Verify(tenant);
        return new AuditReportView(verification.IsValid, verification.Verified, records);
    }

    /// <summary>Runs a parsed audit search and projects the matching records, newest first.</summary>
    /// <param name="audit">The audit engine.</param>
    /// <param name="query">The query to run (its tenant scopes the search).</param>
    /// <returns>The matching records and their count.</returns>
    public static AuditSearchView SearchAudit(AuditEngine audit, AuditQuery query)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(query);

        var records = audit.Search(query)
            .OrderByDescending(record => record.Sequence)
            .Select(ToAuditView)
            .ToArray();

        return new AuditSearchView(records.Length, records);
    }

    /// <summary>
    /// Parses the audit-search filters from a request into an <see cref="AuditQuery"/>, or reports why they were
    /// rejected. Every filter is optional and they combine with AND; an unrecognised category, action, severity,
    /// result or timestamp is a rejection (rather than being silently ignored, which would mislead). The limit is
    /// clamped to a sane maximum so a search can never ask for an unbounded page.
    /// </summary>
    /// <param name="tenant">The tenant the search is scoped to.</param>
    /// <param name="category">The audit category to match, if any.</param>
    /// <param name="action">The audit action to match, if any.</param>
    /// <param name="severity">The minimum severity to match, if any.</param>
    /// <param name="result">The result to match, if any.</param>
    /// <param name="actor">The actor id to match, if any.</param>
    /// <param name="from">The start of the time window (inclusive), if any.</param>
    /// <param name="to">The end of the time window (inclusive), if any.</param>
    /// <param name="contains">A substring the message must contain, if any.</param>
    /// <param name="limit">The maximum number of records to return; clamped to <see cref="MaxAuditSearchLimit"/>.</param>
    /// <returns>The parse outcome: a query, or the rejection reason.</returns>
    public static AuditQueryParse ParseAuditQuery(
        string tenant, string? category, string? action, string? severity, string? result,
        string? actor, string? from, string? to, string? contains, int? limit)
    {
        if (string.IsNullOrWhiteSpace(tenant))
        {
            return new AuditQueryParse(null, "This endpoint is tenant-scoped; the request did not resolve a tenant.");
        }

        if (!TryEnum<AuditCategory>(category, out var categoryValue, out var error)
            || !TryEnum<AuditAction>(action, out var actionValue, out error)
            || !TryEnum<AuditSeverity>(severity, out var severityValue, out error)
            || !TryEnum<AuditResult>(result, out var resultValue, out error)
            || !TryDate(from, out var fromValue, out error)
            || !TryDate(to, out var toValue, out error))
        {
            return new AuditQueryParse(null, error);
        }

        var query = new AuditQuery
        {
            Tenant = tenant,
            Category = categoryValue,
            Action = actionValue,
            MinimumSeverity = severityValue,
            Result = resultValue,
            ActorId = Trimmed(actor),
            MessageContains = Trimmed(contains),
            FromUtc = fromValue,
            ToUtc = toValue,
            Limit = Math.Clamp(limit ?? MaxAuditSearchLimit, 1, MaxAuditSearchLimit),
        };

        return new AuditQueryParse(query, null);
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

    private static AuditView ToAuditView(AuditRecord record) => new(
        record.Sequence,
        record.Category.ToString(),
        record.Action.ToString(),
        record.Severity.ToString(),
        record.Result.ToString(),
        record.Actor.DisplayName ?? record.Actor.Id,
        record.Message,
        record.OccurredOnUtc);

    // Parses an optional enum filter case-insensitively. An absent value is fine (no filter); a value that does not
    // name a defined member is a rejection, so a mistyped filter is reported rather than silently ignored.
    private static bool TryEnum<T>(string? value, out T? parsed, out string? error)
        where T : struct, Enum
    {
        parsed = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<T>(value, ignoreCase: true, out var resolved) && Enum.IsDefined(resolved))
        {
            parsed = resolved;
            return true;
        }

        error = string.Create(CultureInfo.InvariantCulture, $"'{value}' is not a valid {typeof(T).Name}.");
        return false;
    }

    // Parses an optional timestamp filter, round-trip aware. An absent value is fine; an unparseable one is rejected.
    private static bool TryDate(string? value, out DateTimeOffset? parsed, out string? error)
    {
        parsed = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resolved))
        {
            parsed = resolved;
            return true;
        }

        error = string.Create(CultureInfo.InvariantCulture, $"'{value}' is not a valid timestamp.");
        return false;
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PluginView ToView(PluginInstance instance) => new(
        instance.PluginKey,
        instance.Version.ToString(),
        instance.PreviousVersion?.ToString(),
        instance.Status.ToString(),
        instance.Enabled,
        instance.FailureReason,
        instance.StartedUtc);
}
