using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Events;

namespace FactoryOS.Plugins.Workflow.Security.Integration;

/// <summary>
/// What the security engine is measured by.
/// <para>
/// The definitions live here rather than in the monitoring engine's own catalogue, because this commit may not
/// modify that engine — and it turns out not to need to. Monitoring's repository takes definitions from
/// anybody, so a new area registers its own and the collector accepts them exactly as it accepts the built-in
/// thirteen.
/// </para>
/// <para>
/// The one compromise: <c>MetricCategory</c> has no <c>Security</c> value, and adding one would have meant
/// editing the monitoring engine. Security metrics are filed under
/// <see cref="MetricCategory.Infrastructure"/> — defensible, since security is platform infrastructure, and it
/// interferes with nothing, since health checks read named metric keys rather than whole categories. A future
/// commit that is allowed to touch monitoring should give security a category of its own.
/// </para>
/// </summary>
public static class SecurityMetricCollection
{
    /// <summary>Requests that were permitted.</summary>
    public const string AuthorizationsGranted = "security.authorization.granted";

    /// <summary>Requests that were refused.</summary>
    public const string AuthorizationsDenied = "security.authorization.denied";

    /// <summary>Principals that were established.</summary>
    public const string AuthenticationsSucceeded = "security.authentication.succeeded";

    /// <summary>Principals that could not be established.</summary>
    public const string AuthenticationsFailed = "security.authentication.failed";

    /// <summary>Sessions that were opened.</summary>
    public const string SessionsCreated = "security.session.created";

    /// <summary>Sessions that stopped being usable.</summary>
    public const string SessionsEnded = "security.session.ended";

    /// <summary>Violations that were recorded.</summary>
    public const string Violations = "security.violation";

    /// <summary>Incidents that were raised.</summary>
    public const string Incidents = "security.incident";

    /// <summary>The dimension label carrying why a decision or a session went the way it did.</summary>
    public const string ReasonLabel = "reason";

    /// <summary>The dimension label carrying the permission a decision was about.</summary>
    public const string PermissionLabel = "permission";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Counter(AuthorizationsGranted, "Requests permitted.", PermissionLabel),
        Counter(AuthorizationsDenied, "Requests refused.", PermissionLabel, ReasonLabel),
        Counter(AuthenticationsSucceeded, "Principals established."),
        Counter(AuthenticationsFailed, "Principals that could not be established."),
        Counter(SessionsCreated, "Sessions opened."),
        Counter(SessionsEnded, "Sessions that stopped being usable.", ReasonLabel),
        Counter(Violations, "Security violations recorded.", ReasonLabel),
        Counter(Incidents, "Security incidents raised.", ReasonLabel),
    ];

    private static MetricDefinition Counter(string key, string description, params string[] dimensions) =>
        new(key, MetricCategory.Infrastructure, MetricKind.Counter, "count", description)
        {
            Dimensions = dimensions,
        };
}

/// <summary>
/// Turns security events into measurements.
/// <para>
/// Refusals are sliced by <b>reason</b> as well as by permission, which is the slice that matters: a hundred
/// <c>MissingPermission</c> denials is somebody's role being wrong, and a single <c>TenantMismatch</c> is
/// something else entirely. A counter that merged them would report the second as noise inside the first.
/// </para>
/// </summary>
public sealed class SecurityMonitoringBridge : ISecurityEventSink
{
    private readonly MonitoringEngine _monitoring;

    /// <summary>Initializes a new instance of the <see cref="SecurityMonitoringBridge"/> class.</summary>
    /// <param name="monitoring">The monitoring engine.</param>
    public SecurityMonitoringBridge(MonitoringEngine monitoring)
    {
        ArgumentNullException.ThrowIfNull(monitoring);
        _monitoring = monitoring;

        foreach (var definition in SecurityMetricCollection.Definitions)
        {
            _monitoring.Register(definition);
        }
    }

    /// <inheritdoc />
    public void Publish(SecurityEvent securityEvent)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);

        var tenant = securityEvent.Tenant;
        var at = securityEvent.OccurredOnUtc;
        var correlation = new MetricCorrelation(
            securityEvent.Correlation.CorrelationId,
            securityEvent.Correlation.TraceId,
            securityEvent.Correlation.RequestId);

        switch (securityEvent)
        {
            case AuthorizationSucceeded granted:
                _monitoring.Count(
                    tenant,
                    SecurityMetricCollection.AuthorizationsGranted,
                    Permission(granted.Decision.Permission),
                    correlation,
                    at);
                break;

            case AuthorizationFailed refused:
                _monitoring.Count(
                    tenant,
                    SecurityMetricCollection.AuthorizationsDenied,
                    Permission(refused.Decision.Permission)
                        .With(SecurityMetricCollection.ReasonLabel, refused.Decision.Reason.ToString()),
                    correlation,
                    at);
                break;

            case AuthenticationSucceeded:
                _monitoring.Count(
                    tenant, SecurityMetricCollection.AuthenticationsSucceeded, null, correlation, at);
                break;

            case AuthenticationFailed:
                _monitoring.Count(
                    tenant, SecurityMetricCollection.AuthenticationsFailed, null, correlation, at);
                break;

            case SessionCreated:
                _monitoring.Count(tenant, SecurityMetricCollection.SessionsCreated, null, correlation, at);
                break;

            case SessionExpired session:
                _monitoring.Count(
                    tenant,
                    SecurityMetricCollection.SessionsEnded,
                    Reason(session.Reason.ToString()),
                    correlation,
                    at);
                break;

            case SecurityViolationDetected violation:
                _monitoring.Count(
                    tenant,
                    SecurityMetricCollection.Violations,
                    Reason(violation.Violation.Kind.ToString()),
                    correlation,
                    at);
                break;

            case SecurityIncidentCreated incident:
                _monitoring.Count(
                    tenant,
                    SecurityMetricCollection.Incidents,
                    Reason(incident.Incident.Kind.ToString()),
                    correlation,
                    at);
                break;

            default:
                // Granting and revoking a permission is an administrative change, not throughput. The audit
                // trail is where "who changed what" belongs; a counter of it would measure nothing useful.
                break;
        }
    }

    private static MetricDimension Permission(string permission) =>
        MetricDimension.Of(MetricLabel.Of(SecurityMetricCollection.PermissionLabel, permission));

    private static MetricDimension Reason(string reason) =>
        MetricDimension.Of(MetricLabel.Of(SecurityMetricCollection.ReasonLabel, reason));
}
