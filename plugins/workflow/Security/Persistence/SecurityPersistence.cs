using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Security.Domain;

namespace FactoryOS.Plugins.Workflow.Security.Persistence;

/// <summary>
/// The registry of what the platform is configured to allow: policies, roles, and the permissions granted
/// directly to principals.
/// </summary>
public interface ISecurityRepository
{
    /// <summary>Registers a policy, replacing any policy with the same key.</summary>
    /// <param name="policy">The policy.</param>
    void RegisterPolicy(SecurityPolicy policy);

    /// <summary>Registers a role, replacing any role with the same key.</summary>
    /// <param name="role">The role.</param>
    void RegisterRole(SecurityRole role);

    /// <summary>Grants a permission directly to a principal within a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="permission">The permission.</param>
    void Grant(string tenant, string subject, string permission);

    /// <summary>Takes a directly granted permission away.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="permission">The permission.</param>
    /// <returns><see langword="true"/> when the principal held it.</returns>
    bool Revoke(string tenant, string subject, string permission);

    /// <summary>Gets the policies that apply to a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The policies.</returns>
    IReadOnlyList<SecurityPolicy> PoliciesFor(string tenant);

    /// <summary>Gets a role by key.</summary>
    /// <param name="key">The role key.</param>
    /// <returns>The role, or <see langword="null"/> when it is not registered.</returns>
    SecurityRole? FindRole(string key);

    /// <summary>Gets every registered role.</summary>
    /// <returns>The roles.</returns>
    IReadOnlyList<SecurityRole> Roles();

    /// <summary>Gets the permissions granted directly to a principal within a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <returns>The permission strings.</returns>
    IReadOnlyList<string> GrantsFor(string tenant, string subject);
}

/// <summary>An in-memory <see cref="ISecurityRepository"/>.</summary>
public sealed class InMemorySecurityRepository : ISecurityRepository
{
    private readonly ConcurrentDictionary<string, SecurityPolicy> _policies = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SecurityRole> _roles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _grants = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void RegisterPolicy(SecurityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policies[policy.Key] = policy;
    }

    /// <inheritdoc />
    public void RegisterRole(SecurityRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        _roles[role.Key] = role;
    }

    /// <inheritdoc />
    public void Grant(string tenant, string subject, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        // Parsed on the way in, so a malformed grant is refused where somebody can still fix it rather than
        // silently never matching anything at the moment it is relied on.
        var parsed = SecurityPermission.Parse(permission);
        var grants = _grants.GetOrAdd(Key(tenant, subject), _ => new HashSet<string>(StringComparer.Ordinal));
        lock (grants)
        {
            grants.Add(parsed.Value);
        }
    }

    /// <inheritdoc />
    public bool Revoke(string tenant, string subject, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        if (!_grants.TryGetValue(Key(tenant, subject), out var grants))
        {
            return false;
        }

        var parsed = SecurityPermission.Parse(permission);
        lock (grants)
        {
            return grants.Remove(parsed.Value);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityPolicy> PoliciesFor(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _policies.Values
            .Where(policy => policy.AppliesTo(tenant))
            .OrderBy(policy => policy.Key, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public SecurityRole? FindRole(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _roles.TryGetValue(key, out var role) ? role : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityRole> Roles() =>
        _roles.Values.OrderBy(role => role.Key, StringComparer.Ordinal).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<string> GrantsFor(string tenant, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        if (!_grants.TryGetValue(Key(tenant, subject), out var grants))
        {
            return [];
        }

        lock (grants)
        {
            return grants.OrderBy(grant => grant, StringComparer.Ordinal).ToArray();
        }
    }

    // The tenant is part of the key rather than a filter, so a grant made in one tenant cannot be read in
    // another even by a caller that asks wrongly.
    private static string Key(string tenant, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        return $"{tenant}|{subject}";
    }
}

/// <summary>The store of what actually happened: violations and the incidents they added up to.</summary>
public interface ISecurityStore
{
    /// <summary>Records a violation.</summary>
    /// <param name="violation">The violation.</param>
    void Append(SecurityViolation violation);

    /// <summary>Records an incident.</summary>
    /// <param name="incident">The incident.</param>
    void Append(SecurityIncident incident);

    /// <summary>Gets the violations of one kind a principal produced since an instant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="kind">The kind of violation.</param>
    /// <param name="sinceUtc">The start of the window, exclusive.</param>
    /// <returns>The violations, in time order.</returns>
    IReadOnlyList<SecurityViolation> ViolationsSince(
        string tenant, string subject, SecurityViolationKind kind, DateTimeOffset sinceUtc);

    /// <summary>Gets a tenant's violations, newest last.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The violations.</returns>
    IReadOnlyList<SecurityViolation> Violations(string tenant);

    /// <summary>Gets a tenant's incidents, newest last.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The incidents.</returns>
    IReadOnlyList<SecurityIncident> Incidents(string tenant);
}

/// <summary>An in-memory <see cref="ISecurityStore"/>.</summary>
public sealed class InMemorySecurityStore : ISecurityStore
{
    private readonly List<SecurityViolation> _violations = [];
    private readonly List<SecurityIncident> _incidents = [];
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public void Append(SecurityViolation violation)
    {
        ArgumentNullException.ThrowIfNull(violation);
        lock (_gate)
        {
            _violations.Add(violation);
        }
    }

    /// <inheritdoc />
    public void Append(SecurityIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        lock (_gate)
        {
            _incidents.Add(incident);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityViolation> ViolationsSince(
        string tenant, string subject, SecurityViolationKind kind, DateTimeOffset sinceUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        lock (_gate)
        {
            return _violations
                .Where(violation =>
                    string.Equals(violation.Tenant, tenant, StringComparison.Ordinal)
                    && string.Equals(violation.Subject, subject, StringComparison.Ordinal)
                    && violation.Kind == kind
                    && violation.OccurredOnUtc > sinceUtc)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityViolation> Violations(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        lock (_gate)
        {
            return _violations
                .Where(violation => string.Equals(violation.Tenant, tenant, StringComparison.Ordinal))
                .ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityIncident> Incidents(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        lock (_gate)
        {
            return _incidents
                .Where(incident => string.Equals(incident.Tenant, tenant, StringComparison.Ordinal))
                .ToArray();
        }
    }
}

/// <summary>The store of open and ended sessions.</summary>
public interface ISessionRepository
{
    /// <summary>Records a session.</summary>
    /// <param name="session">The session.</param>
    void Add(SecuritySession session);

    /// <summary>Gets a session by id.</summary>
    /// <param name="id">The session identifier.</param>
    /// <returns>The session, or <see langword="null"/> when it is unknown.</returns>
    SecuritySession? Find(string id);

    /// <summary>Gets a principal's sessions within a tenant, oldest first.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <param name="openOnly">Whether to return only sessions that have not ended.</param>
    /// <returns>The sessions.</returns>
    IReadOnlyList<SecuritySession> ForSubject(string tenant, string subject, bool openOnly = true);

    /// <summary>Gets a tenant's sessions, oldest first.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The sessions.</returns>
    IReadOnlyList<SecuritySession> ForTenant(string tenant);
}

/// <summary>An in-memory <see cref="ISessionRepository"/>.</summary>
public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly ConcurrentDictionary<string, SecuritySession> _sessions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Add(SecuritySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions[session.Id] = session;
    }

    /// <inheritdoc />
    public SecuritySession? Find(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _sessions.TryGetValue(id, out var session) ? session : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<SecuritySession> ForSubject(string tenant, string subject, bool openOnly = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return _sessions.Values
            .Where(session =>
                string.Equals(session.Tenant, tenant, StringComparison.Ordinal)
                && string.Equals(session.Subject, subject, StringComparison.Ordinal)
                && (!openOnly || session.EndedOnUtc is null))
            .OrderBy(session => session.CreatedOnUtc)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<SecuritySession> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _sessions.Values
            .Where(session => string.Equals(session.Tenant, tenant, StringComparison.Ordinal))
            .OrderBy(session => session.CreatedOnUtc)
            .ToArray();
    }
}

/// <summary>
/// The store of issued tokens. It is the authority on whether a token is still good — which is what makes
/// revocation immediate rather than a wait for expiry.
/// </summary>
public interface ITokenRepository
{
    /// <summary>Records an issued token.</summary>
    /// <param name="token">The token.</param>
    void Add(SecurityToken token);

    /// <summary>Gets a token by the handle a caller presents.</summary>
    /// <param name="handle">The handle.</param>
    /// <returns>The token, or <see langword="null"/> when it is unknown.</returns>
    SecurityToken? Find(string handle);

    /// <summary>Gets the tokens issued to a principal within a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The principal.</param>
    /// <returns>The tokens, oldest first.</returns>
    IReadOnlyList<SecurityToken> ForSubject(string tenant, string subject);

    /// <summary>Gets the tokens bound to a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The tokens.</returns>
    IReadOnlyList<SecurityToken> ForSession(string sessionId);
}

/// <summary>An in-memory <see cref="ITokenRepository"/>.</summary>
public sealed class InMemoryTokenRepository : ITokenRepository
{
    private readonly ConcurrentDictionary<string, SecurityToken> _tokens = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Add(SecurityToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        _tokens[token.Handle] = token;
    }

    /// <inheritdoc />
    public SecurityToken? Find(string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        return _tokens.TryGetValue(handle, out var token) ? token : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityToken> ForSubject(string tenant, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return _tokens.Values
            .Where(token =>
                string.Equals(token.Tenant, tenant, StringComparison.Ordinal)
                && string.Equals(token.Subject, subject, StringComparison.Ordinal))
            .OrderBy(token => token.IssuedOnUtc)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<SecurityToken> ForSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return _tokens.Values
            .Where(token => string.Equals(token.SessionId, sessionId, StringComparison.Ordinal))
            .OrderBy(token => token.IssuedOnUtc)
            .ToArray();
    }
}
