using System.Linq;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Integration;
using FactoryOS.Plugins.Runtime.Security;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Execution;

namespace FactoryOS.Api.Composition;

// The production adapters that map the plugin runtime's ports onto the platform engines. The plugin runtime
// depends only on its own ports (IPluginAuthorizer, IPluginAuditSink, IPluginMetricSink) and never on an
// engine; the composition root supplies these adapters so a plugin's authorization, audit and metrics flow
// through the platform's security, audit and monitoring engines. They are the canonical bindings — the plugin
// runtime's integration tests exercise these same types.

/// <summary>Maps the plugin runtime's authorization port onto the platform's security engine.</summary>
public sealed class SecurityEnginePluginAuthorizer : IPluginAuthorizer
{
    private readonly SecurityEngine _security;
    private readonly IDateTimeProvider _clock;

    /// <summary>Creates the adapter over the given security engine and clock.</summary>
    /// <param name="security">The platform security engine that decides authorization.</param>
    /// <param name="clock">The clock used to stamp the synthesized principal's identity.</param>
    public SecurityEnginePluginAuthorizer(SecurityEngine security, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(clock);
        _security = security;
        _clock = clock;
    }

    /// <inheritdoc />
    public PluginAuthorization Authorize(
        PluginCaller? caller, PluginInstance instance, PluginPermission required)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (caller is null)
        {
            return PluginAuthorization.Deny(
                PluginAuthorizationReason.NoCaller, "The request named nobody.");
        }

        var principal = new SecurityPrincipal(
            caller.Subject,
            caller.Tenant,
            new SecurityIdentity("plugin-runtime", _clock.UtcNow));

        var decision = _security.Authorize(principal, required.ToString());
        if (decision.IsAllowed)
        {
            return PluginAuthorization.Allow();
        }

        var reason = decision.Reason switch
        {
            SecurityDecisionReason.TenantMismatch => PluginAuthorizationReason.TenantMismatch,
            SecurityDecisionReason.NotAuthenticated => PluginAuthorizationReason.NotAuthenticated,
            _ => PluginAuthorizationReason.MissingPermission,
        };

        return PluginAuthorization.Deny(reason, decision.Description);
    }
}

/// <summary>Maps the plugin runtime's audit port onto the platform's audit engine.</summary>
public sealed class AuditEnginePluginSink : IPluginAuditSink
{
    private readonly AuditEngine _audit;

    /// <summary>Creates the adapter over the given audit engine.</summary>
    /// <param name="audit">The platform audit engine that receives plugin lifecycle records.</param>
    public AuditEnginePluginSink(AuditEngine audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        _audit = audit;
    }

    /// <inheritdoc />
    public void Write(PluginAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // The audit engine already speaks about plugin lifecycle: AuditCategory.Plugin and a ready-made
        // entry. Nothing had to be added to it, and nothing was.
        var outcome = entry.Succeeded ? entry.Phase.ToString() : $"{entry.Phase} refused: {entry.FailureReason}";

        _audit.Record(AuditEntries.PluginOperation(
            entry.Tenant,
            entry.PluginKey,
            outcome,
            entry.Subject is null ? null : AuditActor.User(entry.Subject)));
    }
}

/// <summary>Maps the plugin runtime's metric port onto the platform's monitoring engine.</summary>
public sealed class MonitoringEnginePluginSink : IPluginMetricSink
{
    private readonly MonitoringEngine _monitoring;

    /// <summary>Creates the adapter over the given monitoring engine, registering the plugin metric series.</summary>
    /// <param name="monitoring">The platform monitoring engine that receives plugin runtime measurements.</param>
    public MonitoringEnginePluginSink(MonitoringEngine monitoring)
    {
        ArgumentNullException.ThrowIfNull(monitoring);
        _monitoring = monitoring;

        foreach (var (key, kind, unit) in new[]
                 {
                     (PluginMetricNames.Transitions, MetricKind.Counter, "transitions"),
                     (PluginMetricNames.TransitionDuration, MetricKind.Duration, "ms"),
                     (PluginMetricNames.Failures, MetricKind.Counter, "transitions"),
                     (PluginMetricNames.Installs, MetricKind.Counter, "plugins"),
                     (PluginMetricNames.Starts, MetricKind.Counter, "plugins"),
                     (PluginMetricNames.Stops, MetricKind.Counter, "plugins"),
                     (PluginMetricNames.Updates, MetricKind.Counter, "plugins"),
                     (PluginMetricNames.Rollbacks, MetricKind.Counter, "plugins"),
                     (PluginMetricNames.SandboxRefusals, MetricKind.Counter, "calls"),
                 })
        {
            _monitoring.Register(new MetricDefinition(
                key, MetricCategory.Plugin, kind, unit, $"Plugin runtime: {key}."));
        }
    }

    /// <inheritdoc />
    public void Record(PluginMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        var tenant = measurement.Labels[PluginRuntimeConstants.TenantLabel];
        var dimension = new MetricDimension(
            measurement.Labels
                .Where(label => label.Key != PluginRuntimeConstants.TenantLabel)
                .Select(label => MetricLabel.Of(label.Key, label.Value)));

        _monitoring.Record(
            tenant, measurement.Name, measurement.Value, dimension, timestampUtc: measurement.OccurredUtc);
    }
}
