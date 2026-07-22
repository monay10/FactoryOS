using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Security.Domain;

namespace FactoryOS.Plugins.Workflow.Security.Events;

/// <summary>The base of a security event raised by the runtime and published onto the event bus.</summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OccurredOnUtc">When the event occurred.</param>
/// <param name="Subject">Who the event concerns.</param>
/// <param name="Correlation">The identifiers tying it to the request that caused it.</param>
public abstract record SecurityEvent(
    string Tenant, DateTimeOffset OccurredOnUtc, string Subject, SecurityCorrelation Correlation);

/// <summary>Raised when a principal was established from a token or a session.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Subject">Who.</param>
/// <param name="Correlation">The correlation.</param>
/// <param name="Method">How they authenticated.</param>
/// <param name="SessionId">The session established, when one was.</param>
public sealed record AuthenticationSucceeded(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string Subject,
    SecurityCorrelation Correlation,
    string Method,
    string? SessionId)
    : SecurityEvent(Tenant, OccurredOnUtc, Subject, Correlation);

/// <summary>Raised when a principal could not be established.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Subject">Who was claimed.</param>
/// <param name="Correlation">The correlation.</param>
/// <param name="Method">How they tried.</param>
/// <param name="Reason">Why it failed.</param>
public sealed record AuthenticationFailed(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string Subject,
    SecurityCorrelation Correlation,
    string Method,
    string Reason)
    : SecurityEvent(Tenant, OccurredOnUtc, Subject, Correlation);

/// <summary>Raised when a request was permitted.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Subject">Who asked.</param>
/// <param name="Correlation">The correlation.</param>
/// <param name="Decision">The decision, carrying which rule granted it.</param>
/// <param name="Resource">What was reached for.</param>
public sealed record AuthorizationSucceeded(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string Subject,
    SecurityCorrelation Correlation,
    SecurityDecision Decision,
    string Resource)
    : SecurityEvent(Tenant, OccurredOnUtc, Subject, Correlation);

/// <summary>Raised when a request was refused.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Subject">Who asked.</param>
/// <param name="Correlation">The correlation.</param>
/// <param name="Decision">The decision, carrying why it was refused.</param>
/// <param name="Resource">What was reached for.</param>
public sealed record AuthorizationFailed(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string Subject,
    SecurityCorrelation Correlation,
    SecurityDecision Decision,
    string Resource)
    : SecurityEvent(Tenant, OccurredOnUtc, Subject, Correlation);

/// <summary>Raised when a permission was granted to a principal or a role.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Subject">Who now holds it.</param>
/// <param name="Correlation">The correlation.</param>
/// <param name="Permission">What was granted.</param>
/// <param name="GrantedBy">Who granted it.</param>
public sealed record PermissionGranted(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string Subject,
    SecurityCorrelation Correlation,
    string Permission,
    string GrantedBy)
    : SecurityEvent(Tenant, OccurredOnUtc, Subject, Correlation);

/// <summary>Raised when a permission was taken away.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Subject">Who held it.</param>
/// <param name="Correlation">The correlation.</param>
/// <param name="Permission">What was revoked.</param>
/// <param name="RevokedBy">Who revoked it.</param>
public sealed record PermissionRevoked(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string Subject,
    SecurityCorrelation Correlation,
    string Permission,
    string RevokedBy)
    : SecurityEvent(Tenant, OccurredOnUtc, Subject, Correlation);

/// <summary>Raised when something happened that should not have.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Subject">Who caused it.</param>
/// <param name="Correlation">The correlation.</param>
/// <param name="Violation">What happened.</param>
public sealed record SecurityViolationDetected(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string Subject,
    SecurityCorrelation Correlation,
    SecurityViolation Violation)
    : SecurityEvent(Tenant, OccurredOnUtc, Subject, Correlation);

/// <summary>Raised when violations added up to something somebody should look at.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Subject">Who it concerns.</param>
/// <param name="Correlation">The correlation.</param>
/// <param name="Incident">The incident raised.</param>
public sealed record SecurityIncidentCreated(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string Subject,
    SecurityCorrelation Correlation,
    SecurityIncident Incident)
    : SecurityEvent(Tenant, OccurredOnUtc, Subject, Correlation);

/// <summary>Raised when a session was opened.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Subject">Whose session.</param>
/// <param name="Correlation">The correlation.</param>
/// <param name="SessionId">The session.</param>
/// <param name="ExpiresOnUtc">When it will expire if left alone.</param>
public sealed record SessionCreated(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string Subject,
    SecurityCorrelation Correlation,
    string SessionId,
    DateTimeOffset ExpiresOnUtc)
    : SecurityEvent(Tenant, OccurredOnUtc, Subject, Correlation);

/// <summary>
/// Raised when a session stopped being usable — whether it timed out, was revoked, or was displaced by the
/// concurrent-session limit. One event with a reason rather than three events: everything downstream cares
/// that the session is gone, and only some of it cares why.
/// </summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When.</param>
/// <param name="Subject">Whose session.</param>
/// <param name="Correlation">The correlation.</param>
/// <param name="SessionId">The session.</param>
/// <param name="Reason">Why it ended.</param>
public sealed record SessionExpired(
    string Tenant,
    DateTimeOffset OccurredOnUtc,
    string Subject,
    SecurityCorrelation Correlation,
    string SessionId,
    SessionEndReason Reason)
    : SecurityEvent(Tenant, OccurredOnUtc, Subject, Correlation);

/// <summary>
/// Receives security events raised by the runtime — the seam onto the platform event bus. Like the SLA, audit
/// and monitoring seams this one fans out, so the audit bridge, the monitoring bridge and anything else can
/// observe the same stream without displacing each other.
/// </summary>
public interface ISecurityEventSink
{
    /// <summary>Publishes a security event.</summary>
    /// <param name="securityEvent">The event to publish.</param>
    void Publish(SecurityEvent securityEvent);
}

/// <summary>An in-memory <see cref="ISecurityEventSink"/> that records published events for inspection.</summary>
public sealed class InMemorySecurityEventSink : ISecurityEventSink
{
    private readonly ConcurrentQueue<SecurityEvent> _events = new();

    /// <summary>Gets the published events in order.</summary>
    public IReadOnlyList<SecurityEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Publish(SecurityEvent securityEvent)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);
        _events.Enqueue(securityEvent);
    }
}
