using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Events;

namespace FactoryOS.Plugins.Workflow.Security.Integration;

/// <summary>
/// Turns security events into audit records.
/// <para>
/// The bridge subscribes; the security runtime never calls the audit engine, and the audit engine does not
/// know the security engine exists. That is what keeps the two independently installable — a deployment that
/// wants authorization without an audit trail simply does not attach this — and it is the same shape every
/// other cross-cutting concern in the platform already uses.
/// </para>
/// <para>
/// Every security event is recorded, grants included. A trail that held only refusals could not answer "who
/// read this?", which is the question an auditor actually asks.
/// </para>
/// <para>
/// The audit vocabulary is used as it stands — <c>AuditAction</c> is a small set of stable verbs by design, so
/// an administrative grant is a <see cref="AuditAction.Changed"/> whose <c>EventType</c> says
/// <c>PermissionGranted</c>. Widening that enum to say the same thing twice would have meant modifying the
/// audit engine, which this commit may not do and does not need to.
/// </para>
/// </summary>
public sealed class SecurityAuditBridge : ISecurityEventSink
{
    private readonly AuditEngine _audit;

    /// <summary>Initializes a new instance of the <see cref="SecurityAuditBridge"/> class.</summary>
    /// <param name="audit">The audit engine.</param>
    public SecurityAuditBridge(AuditEngine audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        _audit = audit;
    }

    /// <inheritdoc />
    public void Publish(SecurityEvent securityEvent)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);

        var record = Describe(securityEvent);

        _audit.Record(new AuditEntry
        {
            Category = record.Category,
            Action = record.Action,
            Severity = record.Severity,
            Result = record.Result,
            Actor = AuditActor.User(securityEvent.Subject),
            Target = record.Target,
            Scope = new AuditScope(securityEvent.Tenant, Module: "security"),
            Correlation = new AuditCorrelation(
                securityEvent.Correlation.CorrelationId,
                securityEvent.Correlation.TraceId,
                RequestId: securityEvent.Correlation.RequestId),
            EventType = securityEvent.GetType().Name,
            Message = record.Message,
            OccurredOnUtc = securityEvent.OccurredOnUtc,
        });
    }

    private static AuditRecordShape Describe(SecurityEvent securityEvent) => securityEvent switch
    {
        AuthenticationSucceeded succeeded => new(
            AuditCategory.Authentication,
            AuditAction.SignedIn,
            AuditSeverity.Info,
            AuditResult.Success,
            new AuditTarget(AuditTargetType.User, succeeded.Subject),
            $"{succeeded.Subject} authenticated by {succeeded.Method}."),

        AuthenticationFailed failed => new(
            AuditCategory.Authentication,
            AuditAction.Failed,
            AuditSeverity.Warning,
            AuditResult.Failure,
            new AuditTarget(AuditTargetType.User, failed.Subject),
            $"{failed.Subject} failed to authenticate by {failed.Method}: {failed.Reason}"),

        AuthorizationSucceeded granted => new(
            AuditCategory.Authorization,
            AuditAction.AccessGranted,
            AuditSeverity.Info,
            AuditResult.Success,
            new AuditTarget(AuditTargetType.Role, granted.Decision.Permission, granted.Resource),
            $"{granted.Subject} was granted {granted.Decision.Permission}: {granted.Decision.Description}"),

        AuthorizationFailed refused => new(
            AuditCategory.Authorization,
            AuditAction.AccessDenied,
            // A refusal is security-relevant; an attempt to reach across tenants is in a class of its own.
            refused.Decision.Reason == SecurityDecisionReason.TenantMismatch
                ? AuditSeverity.Critical
                : AuditSeverity.Warning,
            AuditResult.Denied,
            new AuditTarget(AuditTargetType.Role, refused.Decision.Permission, refused.Resource),
            $"{refused.Subject} was refused {refused.Decision.Permission}: {refused.Decision.Description}"),

        PermissionGranted permission => new(
            AuditCategory.Authorization,
            AuditAction.Changed,
            AuditSeverity.Notice,
            AuditResult.Success,
            new AuditTarget(AuditTargetType.User, permission.Subject, permission.Permission),
            $"{permission.GrantedBy} granted {permission.Permission} to {permission.Subject}."),

        PermissionRevoked permission => new(
            AuditCategory.Authorization,
            AuditAction.Changed,
            AuditSeverity.Notice,
            AuditResult.Success,
            new AuditTarget(AuditTargetType.User, permission.Subject, permission.Permission),
            $"{permission.RevokedBy} revoked {permission.Permission} from {permission.Subject}."),

        SecurityViolationDetected violation => new(
            CategoryOf(violation.Violation.Kind),
            AuditAction.Failed,
            SeverityOf(violation.Violation.Risk),
            AuditResult.Failure,
            new AuditTarget(AuditTargetType.User, violation.Subject, violation.Violation.Id.ToString()),
            $"{violation.Violation.Kind}: {violation.Violation.Description}"),

        SecurityIncidentCreated incident => new(
            CategoryOf(incident.Incident.Kind),
            AuditAction.Created,
            AuditSeverity.Critical,
            AuditResult.Failure,
            new AuditTarget(AuditTargetType.User, incident.Subject, incident.Incident.Id.ToString()),
            $"Security incident raised for {incident.Subject}: {incident.Incident.Risk.Rationale}"),

        SessionCreated session => new(
            AuditCategory.Authentication,
            AuditAction.Created,
            AuditSeverity.Info,
            AuditResult.Success,
            new AuditTarget(AuditTargetType.User, session.Subject, session.SessionId),
            $"Session {session.SessionId} opened for {session.Subject}."),

        SessionExpired session => new(
            AuditCategory.Authentication,
            session.Reason is SessionEndReason.Revoked or SessionEndReason.Displaced
                ? AuditAction.Cancelled
                : AuditAction.Expired,
            AuditSeverity.Info,
            AuditResult.Success,
            new AuditTarget(AuditTargetType.User, session.Subject, session.SessionId),
            $"Session {session.SessionId} ended ({session.Reason})."),

        _ => new(
            AuditCategory.Authorization,
            AuditAction.Updated,
            AuditSeverity.Info,
            AuditResult.Success,
            new AuditTarget(AuditTargetType.User, securityEvent.Subject),
            securityEvent.GetType().Name),
    };

    // A refused token or a dead session is an authentication failure; a refused permission is an
    // authorization one. Filing them together would make "who is failing to sign in?" unanswerable.
    private static AuditCategory CategoryOf(SecurityViolationKind kind) => kind switch
    {
        SecurityViolationKind.AuthenticationFailed => AuditCategory.Authentication,
        SecurityViolationKind.InvalidToken => AuditCategory.Authentication,
        SecurityViolationKind.ExpiredSession => AuditCategory.Authentication,
        SecurityViolationKind.ConcurrentSessionLimit => AuditCategory.Authentication,
        _ => AuditCategory.Authorization,
    };

    // The audit severity scale is deliberately coarser than the risk scale, so High and Medium share a level.
    // Flattening downward is the safe direction: it never quietly promotes something to "wake somebody up".
    private static AuditSeverity SeverityOf(SecurityRiskLevel risk) => risk switch
    {
        SecurityRiskLevel.Critical => AuditSeverity.Critical,
        SecurityRiskLevel.High or SecurityRiskLevel.Medium => AuditSeverity.Warning,
        SecurityRiskLevel.Low => AuditSeverity.Notice,
        _ => AuditSeverity.Info,
    };

    private readonly record struct AuditRecordShape(
        AuditCategory Category,
        AuditAction Action,
        AuditSeverity Severity,
        AuditResult Result,
        AuditTarget Target,
        string Message);
}
