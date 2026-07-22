namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>
/// One statement about a principal, made by whoever authenticated it. Claims are the raw material every other
/// part of the model is built from — roles and permissions are themselves claims, read with a known type.
/// <para>
/// The well-known type names mirror the platform's existing <c>FactoryClaimTypes</c> exactly, so a principal
/// assembled from a FactoryOS access token needs no translation table to become a
/// <see cref="SecurityPrincipal"/>.
/// </para>
/// </summary>
/// <param name="Type">The claim type.</param>
/// <param name="Value">The claim value.</param>
/// <param name="Issuer">Who asserted it, when that matters.</param>
public sealed record SecurityClaim(string Type, string Value, string? Issuer = null)
{
    /// <summary>The subject (user identifier) claim.</summary>
    public const string SubjectType = "sub";

    /// <summary>The tenant claim — present on every principal, so tenant is always in scope.</summary>
    public const string TenantType = "factoryos:tenant";

    /// <summary>The organization claim.</summary>
    public const string OrganizationType = "factoryos:org";

    /// <summary>A role claim; one per assigned role.</summary>
    public const string RoleType = "factoryos:role";

    /// <summary>A permission claim; one per effective permission.</summary>
    public const string PermissionType = "factoryos:permission";

    /// <summary>The session claim, present when the principal is bound to a server-side session.</summary>
    public const string SessionType = "factoryos:session";

    /// <summary>Creates a claim, normalising the type so claims never differ by case alone.</summary>
    /// <param name="type">The claim type.</param>
    /// <param name="value">The claim value.</param>
    /// <param name="issuer">Who asserted it.</param>
    /// <returns>The claim.</returns>
    public static SecurityClaim Of(string type, string value, string? issuer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(value);
        return new SecurityClaim(type.Trim().ToLowerInvariant(), value, issuer);
    }
}

/// <summary>
/// A named bundle of permissions. Roles may nest: a role that includes another holds everything the included
/// role holds, which is how "plant manager is everything an operator is, plus more" is expressed once rather
/// than copied.
/// </summary>
/// <param name="Key">The stable role key.</param>
/// <param name="Name">The display name.</param>
public sealed record SecurityRole(string Key, string Name)
{
    /// <summary>Gets the permissions the role carries.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Gets the roles this role includes.</summary>
    public IReadOnlyList<string> Includes { get; init; } = [];

    /// <summary>Gets what the role is for.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Creates a role carrying a set of permissions.</summary>
    /// <param name="key">The role key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="permissions">The permissions it carries.</param>
    /// <returns>The role.</returns>
    public static SecurityRole Of(string key, string name, params string[] permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(permissions);
        return new SecurityRole(key, name) { Permissions = permissions };
    }
}

/// <summary>
/// How a principal proved who it is: the authentication method, when it happened, and by whom it was asserted.
/// <para>
/// The engine does not authenticate. It reads an identity that something upstream established — the platform's
/// identity service, a token issuer, a gateway — and decides what that identity may do. Keeping the two apart
/// is what stops an authorization engine from quietly becoming a second, weaker login system.
/// </para>
/// </summary>
/// <param name="Method">How the principal authenticated (<c>password</c>, <c>token</c>, <c>service</c>, …).</param>
/// <param name="AuthenticatedOnUtc">When it authenticated.</param>
/// <param name="Issuer">Who established the identity.</param>
public sealed record SecurityIdentity(string Method, DateTimeOffset AuthenticatedOnUtc, string? Issuer = null)
{
    /// <summary>The method used when nothing authenticated at all.</summary>
    public const string AnonymousMethod = "anonymous";

    /// <summary>An identity that nothing established.</summary>
    public static SecurityIdentity Anonymous { get; } =
        new(AnonymousMethod, DateTimeOffset.MinValue);

    /// <summary>Gets a value indicating whether anything actually authenticated.</summary>
    public bool IsAuthenticated =>
        !string.Equals(Method, AnonymousMethod, StringComparison.Ordinal);
}

/// <summary>
/// Who is asking. A principal carries its identity, the tenant it belongs to, the roles and permissions it was
/// granted, and every other claim presented about it.
/// <para>
/// <b>The tenant is not optional.</b> It is a required part of constructing a principal rather than a claim
/// that might be missing, because a principal with no tenant is one that a later comparison would have to
/// guess about — and a guess in that position is a cross-tenant read waiting to happen.
/// </para>
/// </summary>
public sealed class SecurityPrincipal
{
    private readonly List<SecurityClaim> _claims;
    private readonly HashSet<string> _roles;
    private readonly List<SecurityPermission> _permissions;

    /// <summary>Initializes a new instance of the <see cref="SecurityPrincipal"/> class.</summary>
    /// <param name="subject">The principal's identifier.</param>
    /// <param name="tenant">The tenant it belongs to.</param>
    /// <param name="identity">How it authenticated.</param>
    /// <param name="claims">Every claim presented about it.</param>
    public SecurityPrincipal(
        string subject,
        string tenant,
        SecurityIdentity? identity = null,
        IEnumerable<SecurityClaim>? claims = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        Subject = subject;
        Tenant = tenant;
        Identity = identity ?? SecurityIdentity.Anonymous;
        _claims = claims is null ? [] : [.. claims];

        _roles = _claims
            .Where(claim => string.Equals(claim.Type, SecurityClaim.RoleType, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _permissions = _claims
            .Where(claim => string.Equals(claim.Type, SecurityClaim.PermissionType, StringComparison.Ordinal))
            .Select(claim => SecurityPermission.TryParse(claim.Value, out var parsed) ? parsed : null)
            .OfType<SecurityPermission>()
            .ToList();
    }

    /// <summary>Gets the principal's identifier.</summary>
    public string Subject { get; }

    /// <summary>Gets the tenant the principal belongs to.</summary>
    public string Tenant { get; }

    /// <summary>Gets how the principal authenticated.</summary>
    public SecurityIdentity Identity { get; }

    /// <summary>Gets a value indicating whether anything actually authenticated this principal.</summary>
    public bool IsAuthenticated => Identity.IsAuthenticated;

    /// <summary>Gets every claim presented about the principal.</summary>
    public IReadOnlyList<SecurityClaim> Claims => _claims;

    /// <summary>Gets the roles the principal holds, as presented.</summary>
    public IReadOnlyCollection<string> Roles => _roles;

    /// <summary>Gets the permissions the principal presented directly, as parsed.</summary>
    public IReadOnlyList<SecurityPermission> Permissions => _permissions;

    /// <summary>Gets the organization the principal belongs to, when it presented one.</summary>
    public string? Organization => FindFirst(SecurityClaim.OrganizationType);

    /// <summary>Gets the session the principal is bound to, when it presented one.</summary>
    public string? SessionId => FindFirst(SecurityClaim.SessionType);

    /// <summary>Builds an anonymous principal for a tenant — one that has proved nothing.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The principal.</returns>
    public static SecurityPrincipal Anonymous(string tenant) =>
        new(SecurityIdentity.AnonymousMethod, tenant, SecurityIdentity.Anonymous);

    /// <summary>Gets the value of the first claim of a type, or <see langword="null"/> when there is none.</summary>
    /// <param name="type">The claim type.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? FindFirst(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return _claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    /// <summary>Gets every value of a claim type.</summary>
    /// <param name="type">The claim type.</param>
    /// <returns>The values.</returns>
    public IReadOnlyList<string> FindAll(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return _claims
            .Where(claim => string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToArray();
    }

    /// <summary>Gets a value indicating whether the principal presented a claim with a given value.</summary>
    /// <param name="type">The claim type.</param>
    /// <param name="value">The value it must carry.</param>
    /// <returns><see langword="true"/> when the claim is present with that value.</returns>
    public bool HasClaim(string type, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(value);
        return _claims.Any(claim =>
            string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase)
            && string.Equals(claim.Value, value, StringComparison.Ordinal));
    }

    /// <summary>Gets a value indicating whether the principal holds a role, as presented.</summary>
    /// <param name="role">The role key.</param>
    /// <returns><see langword="true"/> when the role was presented.</returns>
    public bool HasRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return _roles.Contains(role);
    }
}
