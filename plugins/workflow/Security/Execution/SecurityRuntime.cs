using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Security.Configuration;
using FactoryOS.Plugins.Workflow.Security.Diagnostics;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Events;
using FactoryOS.Plugins.Workflow.Security.Persistence;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>
/// The heart of the security engine: it decides requests, records what happened, notices when refusals add up
/// to something worth looking at, and announces all of it on the event bus.
/// <para>
/// The runtime writes nothing back to any engine it protects. It answers questions and publishes events; the
/// audit trail and the metrics that follow are produced by bridges that <i>subscribe</i>, not by the runtime
/// reaching sideways into the audit or monitoring engines.
/// </para>
/// </summary>
public sealed class SecurityRuntime
{
    private readonly AuthorizationEngine _authorization;
    private readonly SessionManager _sessions;
    private readonly TokenValidator _tokens;
    private readonly ClaimResolver _claims;
    private readonly ISecurityRepository _repository;
    private readonly ISecurityStore _store;
    private readonly SecurityDispatcher _dispatcher;
    private readonly SecurityMetrics _metrics;
    private readonly SecurityEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="SecurityRuntime"/> class.</summary>
    /// <param name="authorization">The authorization engine.</param>
    /// <param name="sessions">The session manager.</param>
    /// <param name="tokens">The token validator.</param>
    /// <param name="claims">The claim resolver.</param>
    /// <param name="repository">The policy, role and grant registry.</param>
    /// <param name="store">The violation and incident store.</param>
    /// <param name="dispatcher">The event dispatcher.</param>
    /// <param name="metrics">The engine's own counters.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public SecurityRuntime(
        AuthorizationEngine authorization,
        SessionManager sessions,
        TokenValidator tokens,
        ClaimResolver claims,
        ISecurityRepository repository,
        ISecurityStore store,
        SecurityDispatcher dispatcher,
        SecurityMetrics metrics,
        SecurityEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _authorization = authorization;
        _sessions = sessions;
        _tokens = tokens;
        _claims = claims;
        _repository = repository;
        _store = store;
        _dispatcher = dispatcher;
        _metrics = metrics;
        _options = options;
        _clock = clock;
    }

    /// <summary>Decides a request, records it, and announces the outcome.</summary>
    /// <param name="context">Everything the decision is made from.</param>
    /// <returns>The decision, carrying why it went the way it did.</returns>
    public SecurityDecision Authorize(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var decision = _authorization.Authorize(context);
        var now = _clock.UtcNow;
        var tenant = context.Scope.Tenant;
        var subject = context.Principal.Subject;

        if (decision.IsAllowed)
        {
            _metrics.RecordGranted();
            if (_options.RecordGrantedDecisions)
            {
                _dispatcher.Publish(new AuthorizationSucceeded(
                    tenant, now, subject, context.Correlation, decision, context.Resource.ToString()));
            }

            return decision;
        }

        _metrics.RecordDenied();
        _dispatcher.Publish(new AuthorizationFailed(
            tenant, now, subject, context.Correlation, decision, context.Resource.ToString()));

        RecordViolation(
            KindOf(decision.Reason),
            tenant,
            subject,
            decision.Description,
            context.Correlation,
            decision.Permission,
            context.Resource.ToString(),
            context.NetworkAddress);

        return decision;
    }

    /// <summary>
    /// Establishes a principal from a token a caller presented.
    /// </summary>
    /// <param name="handle">The token handle.</param>
    /// <param name="tenant">The tenant the request names.</param>
    /// <param name="correlation">The identifiers tying it to the request.</param>
    /// <param name="audience">The audience the request is for.</param>
    /// <returns>
    /// The principal the token stands for, or <see langword="null"/> when the token may not be used. A refusal
    /// is recorded as a violation: a token that is no longer good being presented is exactly the signal that
    /// matters when a credential has leaked.
    /// </returns>
    public SecurityPrincipal? Authenticate(
        string handle, string tenant, SecurityCorrelation? correlation = null, string? audience = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var context = correlation ?? SecurityCorrelation.None;
        var now = _clock.UtcNow;
        var result = _tokens.Validate(handle, tenant, audience);

        if (!result.IsValid)
        {
            var subject = result.Token?.Subject ?? "unknown";
            _metrics.RecordAuthenticationFailure();
            _metrics.RecordTokenRejected();
            _dispatcher.Publish(new AuthenticationFailed(
                tenant, now, subject, context, "token", result.Detail));

            RecordViolation(
                result.Reason == SecurityDecisionReason.SessionNotActive
                    ? SecurityViolationKind.ExpiredSession
                    : SecurityViolationKind.InvalidToken,
                tenant,
                subject,
                result.Detail,
                context);

            return null;
        }

        var token = result.Token!;
        var claims = new List<SecurityClaim>(token.Claims)
        {
            SecurityClaim.Of(SecurityClaim.SubjectType, token.Subject),
            SecurityClaim.Of(SecurityClaim.TenantType, token.Tenant),
        };

        if (token.SessionId is { } sessionId)
        {
            claims.Add(SecurityClaim.Of(SecurityClaim.SessionType, sessionId));

            // Using a token is using the session behind it, so the idle window slides — otherwise a person
            // working steadily through an API would be signed out mid-shift.
            _sessions.Renew(sessionId);
        }

        var principal = new SecurityPrincipal(
            token.Subject,
            token.Tenant,
            new SecurityIdentity("token", token.IssuedOnUtc, token.Issuer),
            claims);

        _metrics.RecordAuthenticated();
        _dispatcher.Publish(new AuthenticationSucceeded(
            tenant, now, token.Subject, context, "token", token.SessionId));

        return _claims.Resolve(principal);
    }

    /// <summary>Opens a session and announces it, along with anything displaced to make room.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="networkAddress">Where it was opened from.</param>
    /// <param name="correlation">The identifiers tying it to the request.</param>
    /// <returns>The session and anything displaced.</returns>
    public SessionCreation CreateSession(
        string tenant, string subject, string? networkAddress = null, SecurityCorrelation? correlation = null)
    {
        var context = correlation ?? SecurityCorrelation.None;
        var now = _clock.UtcNow;
        var creation = _sessions.Create(tenant, subject, networkAddress);

        _metrics.RecordSessionCreated();
        _dispatcher.Publish(new SessionCreated(
            tenant, now, subject, context, creation.Session.Id, creation.Session.IdleExpiresOnUtc));

        foreach (var displaced in creation.Displaced)
        {
            EndSession(displaced, SessionEndReason.Displaced, context, now);
            RecordViolation(
                SecurityViolationKind.ConcurrentSessionLimit,
                tenant,
                subject,
                $"Session {displaced.Id} was displaced by the concurrent-session limit "
                + $"of {_options.MaxConcurrentSessions}.",
                context);
        }

        return creation;
    }

    /// <summary>Ends a session and every token bound to it.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="correlation">The identifiers tying it to the request.</param>
    /// <returns><see langword="true"/> when this call ended it.</returns>
    public bool RevokeSession(string sessionId, SecurityCorrelation? correlation = null)
    {
        var session = _sessions.Revoke(sessionId);
        if (session is null)
        {
            return false;
        }

        // A revoked session whose tokens still worked would be a sign-out that signed nobody out.
        _tokens.RevokeForSession(sessionId, "The session was revoked.");
        EndSession(session, SessionEndReason.Revoked, correlation ?? SecurityCorrelation.None, _clock.UtcNow);
        return true;
    }

    /// <summary>Retires the sessions in a tenant that have passed one of their clocks, and announces each.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>How many sessions were retired.</returns>
    public int RetireExpiredSessions(string tenant)
    {
        var now = _clock.UtcNow;
        var retired = _sessions.RetireExpired(tenant);

        foreach (var (session, reason) in retired)
        {
            _tokens.RevokeForSession(session.Id, $"The session ended: {reason}.");
            _dispatcher.Publish(new SessionExpired(
                session.Tenant, now, session.Subject, SecurityCorrelation.None, session.Id, reason));
        }

        _metrics.RecordSessionsEnded(retired.Count);
        return retired.Count;
    }

    /// <summary>Issues a token and announces nothing — issuing is not itself an authorization event.</summary>
    /// <param name="subject">The principal.</param>
    /// <param name="tenant">The tenant.</param>
    /// <param name="issuer">Who issued it.</param>
    /// <param name="sessionId">The session it is bound to.</param>
    /// <param name="claims">The claims it carries.</param>
    /// <returns>The token record.</returns>
    public SecurityToken IssueToken(
        string subject,
        string tenant,
        string issuer,
        string? sessionId = null,
        IEnumerable<SecurityClaim>? claims = null)
    {
        var token = _tokens.Issue(subject, tenant, issuer, sessionId, claims);
        _metrics.RecordTokenIssued();
        return token;
    }

    /// <summary>Grants a permission to a principal and announces it.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="grantedBy">Who granted it.</param>
    /// <param name="correlation">The identifiers tying it to the request.</param>
    public void Grant(
        string tenant,
        string subject,
        string permission,
        string grantedBy,
        SecurityCorrelation? correlation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grantedBy);

        _repository.Grant(tenant, subject, permission);
        _dispatcher.Publish(new PermissionGranted(
            tenant, _clock.UtcNow, subject, correlation ?? SecurityCorrelation.None, permission, grantedBy));
    }

    /// <summary>Takes a permission away and announces it.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="revokedBy">Who revoked it.</param>
    /// <param name="correlation">The identifiers tying it to the request.</param>
    /// <returns><see langword="true"/> when the principal held it.</returns>
    public bool RevokePermission(
        string tenant,
        string subject,
        string permission,
        string revokedBy,
        SecurityCorrelation? correlation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revokedBy);

        if (!_repository.Revoke(tenant, subject, permission))
        {
            return false;
        }

        _dispatcher.Publish(new PermissionRevoked(
            tenant, _clock.UtcNow, subject, correlation ?? SecurityCorrelation.None, permission, revokedBy));
        return true;
    }

    /// <summary>Gets a tenant's recorded violations.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The violations.</returns>
    public IReadOnlyList<SecurityViolation> Violations(string tenant) => _store.Violations(tenant);

    /// <summary>Gets a tenant's raised incidents.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The incidents.</returns>
    public IReadOnlyList<SecurityIncident> Incidents(string tenant) => _store.Incidents(tenant);

    private void EndSession(
        SecuritySession session, SessionEndReason reason, SecurityCorrelation correlation, DateTimeOffset now)
    {
        _tokens.RevokeForSession(session.Id, $"The session ended: {reason}.");
        _metrics.RecordSessionsEnded(1);
        _dispatcher.Publish(new SessionExpired(
            session.Tenant, now, session.Subject, correlation, session.Id, reason));
    }

    private void RecordViolation(
        SecurityViolationKind kind,
        string tenant,
        string subject,
        string description,
        SecurityCorrelation correlation,
        string? permission = null,
        string? resource = null,
        string? networkAddress = null)
    {
        var now = _clock.UtcNow;
        var since = now - _options.ViolationWindow;
        var priorCount = _store.ViolationsSince(tenant, subject, kind, since).Count;

        var violation = SecurityViolation.Of(kind, tenant, subject, now, description) with
        {
            Permission = permission,
            Resource = resource,
            NetworkAddress = networkAddress,
            Correlation = correlation,
            Risk = SecurityRisk.From(kind, priorCount + 1).Level,
        };

        _store.Append(violation);
        _metrics.RecordViolation();
        _dispatcher.Publish(new SecurityViolationDetected(tenant, now, subject, correlation, violation));

        // Incidents are raised on the crossing, not on every violation past it. Raising one per violation
        // afterwards would bury the moment the pattern actually appeared.
        var total = priorCount + 1;
        if (total != _options.IncidentThreshold)
        {
            return;
        }

        var window = _store.ViolationsSince(tenant, subject, kind, since);
        var incident = SecurityIncident.Raise(
            tenant, subject, kind, SecurityRisk.From(kind, total), now, window);

        _store.Append(incident);
        _metrics.RecordIncident();
        _dispatcher.Publish(new SecurityIncidentCreated(tenant, now, subject, correlation, incident));
    }

    private static SecurityViolationKind KindOf(SecurityDecisionReason reason) => reason switch
    {
        SecurityDecisionReason.TenantMismatch => SecurityViolationKind.CrossTenantAccess,
        SecurityDecisionReason.SessionNotActive => SecurityViolationKind.ExpiredSession,
        SecurityDecisionReason.TokenNotValid => SecurityViolationKind.InvalidToken,
        SecurityDecisionReason.NotAuthenticated => SecurityViolationKind.AuthenticationFailed,
        _ => SecurityViolationKind.AuthorizationDenied,
    };
}
