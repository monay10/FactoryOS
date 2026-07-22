using FactoryOS.Plugins.Workflow.Security.Domain;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>
/// Answers the narrow question: does this principal hold this permission?
/// <para>
/// Only the wildcard grammar decides. A grant of <c>workflow.*</c> covers <c>workflow.start</c>, and <c>*</c>
/// covers everything — nothing else widens a grant, and in particular no role name is special. A system where
/// being called "admin" implied anything would be one where a typo in a role name is a privilege escalation.
/// </para>
/// </summary>
public sealed class PermissionEvaluator
{
    private readonly ClaimResolver _claims;

    /// <summary>Initializes a new instance of the <see cref="PermissionEvaluator"/> class.</summary>
    /// <param name="claims">The claim resolver that expands roles and grants.</param>
    public PermissionEvaluator(ClaimResolver claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        _claims = claims;
    }

    /// <summary>Gets a value indicating whether a principal holds a permission.</summary>
    /// <param name="principal">The principal, resolved or not.</param>
    /// <param name="permission">The concrete permission being asked for.</param>
    /// <returns><see langword="true"/> when something the principal holds covers it.</returns>
    public bool HasPermission(SecurityPrincipal principal, string permission)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return HasPermission(principal, SecurityPermission.Parse(permission));
    }

    /// <summary>Gets a value indicating whether a principal holds a permission.</summary>
    /// <param name="principal">The principal, resolved or not.</param>
    /// <param name="permission">The concrete permission being asked for.</param>
    /// <returns><see langword="true"/> when something the principal holds covers it.</returns>
    public bool HasPermission(SecurityPrincipal principal, SecurityPermission permission)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(permission);

        // An unauthenticated principal holds nothing, whatever claims it presented — otherwise anything able
        // to fabricate a permission claim would hold that permission.
        if (!principal.IsAuthenticated)
        {
            return false;
        }

        return _claims.Resolve(principal).Permissions.Any(held => held.Grants(permission));
    }

    /// <summary>Gets the permission a principal holds that covers a request, if any.</summary>
    /// <param name="principal">The principal.</param>
    /// <param name="permission">The concrete permission being asked for.</param>
    /// <returns>The covering grant, or <see langword="null"/> when there is none.</returns>
    public SecurityPermission? CoveringGrant(SecurityPrincipal principal, SecurityPermission permission)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(permission);

        if (!principal.IsAuthenticated)
        {
            return null;
        }

        // The narrowest covering grant is reported, so "why am I allowed?" names the specific permission
        // rather than a wildcard that happens to sort first.
        return _claims.Resolve(principal).Permissions
            .Where(held => held.Grants(permission))
            .OrderBy(held => held.IsWildcard ? 1 : 0)
            .ThenBy(held => held.Value, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>Gets every permission a principal effectively holds.</summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The permission strings, ordered.</returns>
    public IReadOnlyList<string> EffectivePermissions(SecurityPrincipal principal) =>
        _claims.EffectivePermissions(principal);
}
