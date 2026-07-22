using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Security.Diagnostics;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Persistence;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>
/// The public entry point to the security engine. It registers policies, roles and grants; decides requests;
/// manages sessions and tokens; and reports the violations and incidents it recorded.
/// <para>
/// Security is a platform service, and the dependency arrow points one way: engines speak the shared security
/// vocabulary — a permission string, a principal, a claim — and this engine evaluates it. No engine holds a
/// reference to <see cref="SecurityEngine"/>, and deleting the <c>Security/</c> folder would leave every one of
/// them working exactly as before.
/// </para>
/// </summary>
public sealed class SecurityEngine
{
    private readonly SecurityRuntime _runtime;
    private readonly AuthorizationEngine _authorization;
    private readonly PermissionEvaluator _permissions;
    private readonly SessionManager _sessions;
    private readonly TokenValidator _tokens;
    private readonly ISecurityRepository _repository;
    private readonly SecurityMetrics _metrics;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="SecurityEngine"/> class.</summary>
    /// <param name="runtime">The security runtime.</param>
    /// <param name="authorization">The authorization engine.</param>
    /// <param name="permissions">The permission evaluator.</param>
    /// <param name="sessions">The session manager.</param>
    /// <param name="tokens">The token validator.</param>
    /// <param name="repository">The policy, role and grant registry.</param>
    /// <param name="metrics">The engine's own counters.</param>
    /// <param name="clock">The clock.</param>
    public SecurityEngine(
        SecurityRuntime runtime,
        AuthorizationEngine authorization,
        PermissionEvaluator permissions,
        SessionManager sessions,
        TokenValidator tokens,
        ISecurityRepository repository,
        SecurityMetrics metrics,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(clock);
        _runtime = runtime;
        _authorization = authorization;
        _permissions = permissions;
        _sessions = sessions;
        _tokens = tokens;
        _repository = repository;
        _metrics = metrics;
        _clock = clock;
    }

    /// <summary>Starts assembling a request to decide.</summary>
    /// <returns>A fresh builder, stamped from the platform clock.</returns>
    public SecurityContextBuilder Request() => new(_clock);

    /// <summary>Registers a policy.</summary>
    /// <param name="policy">The policy.</param>
    public void RegisterPolicy(SecurityPolicy policy) => _repository.RegisterPolicy(policy);

    /// <summary>Registers a role.</summary>
    /// <param name="role">The role.</param>
    public void RegisterRole(SecurityRole role) => _repository.RegisterRole(role);

    /// <summary>Gets the registered roles.</summary>
    /// <returns>The roles.</returns>
    public IReadOnlyList<SecurityRole> Roles() => _repository.Roles();

    /// <summary>Gets the policies that apply to a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The policies.</returns>
    public IReadOnlyList<SecurityPolicy> Policies(string tenant) => _repository.PoliciesFor(tenant);

    /// <summary>Decides a request, records it, and announces the outcome.</summary>
    /// <param name="context">Everything the decision is made from.</param>
    /// <returns>The decision, carrying why it went the way it did.</returns>
    public SecurityDecision Authorize(SecurityContext context) => _runtime.Authorize(context);

    /// <summary>Decides a request for a permission, with no particular resource instance in mind.</summary>
    /// <param name="principal">Who is asking.</param>
    /// <param name="permission">The permission being asked for.</param>
    /// <param name="correlation">The identifiers tying it to the request.</param>
    /// <returns>The decision.</returns>
    public SecurityDecision Authorize(
        SecurityPrincipal principal, string permission, SecurityCorrelation? correlation = null)
    {
        var builder = Request().For(principal).Requesting(permission);
        if (correlation is not null)
        {
            builder.CorrelatedBy(correlation);
        }

        return Authorize(builder.Build());
    }

    /// <summary>
    /// Decides a request without recording or announcing anything — for a caller that needs to know whether a
    /// button should be drawn, rather than whether an operation may proceed. Rendering a screen would
    /// otherwise fill the trail with denials nobody attempted.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The decision.</returns>
    public SecurityDecision Preview(SecurityContext context) => _authorization.Authorize(context);

    /// <summary>Gets a value indicating whether a principal holds a permission.</summary>
    /// <param name="principal">The principal.</param>
    /// <param name="permission">The permission.</param>
    /// <returns><see langword="true"/> when something the principal holds covers it.</returns>
    public bool HasPermission(SecurityPrincipal principal, string permission) =>
        _permissions.HasPermission(principal, permission);

    /// <summary>Gets every permission a principal effectively holds.</summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The permission strings, ordered.</returns>
    public IReadOnlyList<string> EffectivePermissions(SecurityPrincipal principal) =>
        _permissions.EffectivePermissions(principal);

    /// <summary>Grants a permission to a principal.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="grantedBy">Who granted it.</param>
    public void Grant(string tenant, string subject, string permission, string grantedBy) =>
        _runtime.Grant(tenant, subject, permission, grantedBy);

    /// <summary>Takes a permission away.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="revokedBy">Who revoked it.</param>
    /// <returns><see langword="true"/> when the principal held it.</returns>
    public bool RevokePermission(string tenant, string subject, string permission, string revokedBy) =>
        _runtime.RevokePermission(tenant, subject, permission, revokedBy);

    /// <summary>Establishes a principal from a token a caller presented.</summary>
    /// <param name="handle">The token handle.</param>
    /// <param name="tenant">The tenant the request names.</param>
    /// <param name="correlation">The identifiers tying it to the request.</param>
    /// <returns>The principal, or <see langword="null"/> when the token may not be used.</returns>
    public SecurityPrincipal? Authenticate(
        string handle, string tenant, SecurityCorrelation? correlation = null) =>
        _runtime.Authenticate(handle, tenant, correlation);

    /// <summary>Issues a token.</summary>
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
        IEnumerable<SecurityClaim>? claims = null) =>
        _runtime.IssueToken(subject, tenant, issuer, sessionId, claims);

    /// <summary>Answers whether a presented token may be used.</summary>
    /// <param name="handle">The handle.</param>
    /// <param name="tenant">The tenant the request names.</param>
    /// <returns>What validation concluded, and why.</returns>
    public TokenValidationResult ValidateToken(string handle, string? tenant = null) =>
        _tokens.Validate(handle, tenant);

    /// <summary>Revokes a token.</summary>
    /// <param name="handle">The handle.</param>
    /// <param name="reason">Why.</param>
    /// <returns><see langword="true"/> when this call revoked it.</returns>
    public bool RevokeToken(string handle, string reason) => _tokens.Revoke(handle, reason) is not null;

    /// <summary>Opens a session, displacing the principal's oldest if it is at its limit.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="networkAddress">Where it was opened from.</param>
    /// <returns>The session and anything displaced.</returns>
    public SessionCreation CreateSession(string tenant, string subject, string? networkAddress = null) =>
        _runtime.CreateSession(tenant, subject, networkAddress);

    /// <summary>Finds a session if it is alive, sliding its idle window forward.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The session, or <see langword="null"/> when it is unknown or finished.</returns>
    public SecuritySession? RenewSession(string sessionId) => _sessions.Renew(sessionId);

    /// <summary>Ends a session and every token bound to it.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns><see langword="true"/> when this call ended it.</returns>
    public bool RevokeSession(string sessionId) => _runtime.RevokeSession(sessionId);

    /// <summary>Ends every open session a principal holds.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <returns>How many sessions this call ended.</returns>
    public int RevokeAllSessions(string tenant, string subject)
    {
        var revoked = 0;
        foreach (var session in _sessions.ActiveSessions(tenant, subject))
        {
            if (_runtime.RevokeSession(session.Id))
            {
                revoked++;
            }
        }

        return revoked;
    }

    /// <summary>Gets a principal's open sessions.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <returns>The sessions, oldest first.</returns>
    public IReadOnlyList<SecuritySession> ActiveSessions(string tenant, string subject) =>
        _sessions.ActiveSessions(tenant, subject);

    /// <summary>Retires the sessions in a tenant that have passed one of their clocks.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>How many were retired.</returns>
    public int RetireExpiredSessions(string tenant) => _runtime.RetireExpiredSessions(tenant);

    /// <summary>Gets a tenant's recorded violations.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The violations.</returns>
    public IReadOnlyList<SecurityViolation> Violations(string tenant) => _runtime.Violations(tenant);

    /// <summary>Gets a tenant's raised incidents.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The incidents.</returns>
    public IReadOnlyList<SecurityIncident> Incidents(string tenant) => _runtime.Incidents(tenant);

    /// <summary>Reads the engine's counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public SecurityMetricsSnapshot Snapshot() => _metrics.Snapshot();
}
