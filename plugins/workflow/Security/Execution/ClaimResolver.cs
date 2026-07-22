using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Persistence;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>
/// Turns the principal that arrived into the principal the engine reasons about: presented claims, plus the
/// roles those roles include, plus the permissions those roles carry, plus whatever was granted to this
/// principal directly.
/// <para>
/// Doing this once, in one place, is the point. If role expansion happened inside the permission check and
/// direct grants were read inside the policy check, the two would eventually disagree about what a principal
/// holds — and the version that decided would be whichever one ran first.
/// </para>
/// </summary>
public sealed class ClaimResolver
{
    private readonly ISecurityRepository _repository;
    private readonly RoleResolver _roles;

    /// <summary>Initializes a new instance of the <see cref="ClaimResolver"/> class.</summary>
    /// <param name="repository">The grant registry.</param>
    /// <param name="roles">The role resolver.</param>
    public ClaimResolver(ISecurityRepository repository, RoleResolver roles)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(roles);
        _repository = repository;
        _roles = roles;
    }

    /// <summary>
    /// Resolves a principal against what the platform knows about it.
    /// </summary>
    /// <param name="principal">The principal as it arrived.</param>
    /// <returns>
    /// A principal carrying every effective role and permission. The original is untouched — resolution is a
    /// projection, so the same request can safely be resolved twice.
    /// </returns>
    public SecurityPrincipal Resolve(SecurityPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var effectiveRoles = _roles.ExpandKeys(principal.Roles);
        var fromRoles = _roles.PermissionsOf(principal.Roles);
        var direct = _repository.GrantsFor(principal.Tenant, principal.Subject);

        var claims = new List<SecurityClaim>(principal.Claims);
        var known = principal.Claims
            .Select(claim => (claim.Type, claim.Value))
            .ToHashSet();

        foreach (var role in effectiveRoles)
        {
            Add(claims, known, SecurityClaim.RoleType, role);
        }

        foreach (var permission in fromRoles.Concat(direct))
        {
            Add(claims, known, SecurityClaim.PermissionType, permission);
        }

        return new SecurityPrincipal(principal.Subject, principal.Tenant, principal.Identity, claims);
    }

    /// <summary>Gets every permission a principal effectively holds, as strings.</summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The permission strings, ordered.</returns>
    public IReadOnlyList<string> EffectivePermissions(SecurityPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return Resolve(principal).Permissions
            .Select(permission => permission.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Add(
        List<SecurityClaim> claims, HashSet<(string Type, string Value)> known, string type, string value)
    {
        var claim = SecurityClaim.Of(type, value);
        if (known.Add((claim.Type, claim.Value)))
        {
            claims.Add(claim);
        }
    }
}
