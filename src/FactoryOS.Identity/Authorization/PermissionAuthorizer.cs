using System.Security.Claims;
using FactoryOS.Identity.Claims;

namespace FactoryOS.Identity.Authorization;

/// <summary>Default <see cref="IPermissionAuthorizer"/> evaluating the principal's permission claims.</summary>
public sealed class PermissionAuthorizer : IPermissionAuthorizer
{
    /// <inheritdoc />
    public bool HasPermission(ClaimsPrincipal principal, string permission)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var requested = Permission.Parse(permission);

        foreach (var claim in principal.FindAll(FactoryClaimTypes.Permission))
        {
            if (TryParse(claim.Value, out var granted) && granted.Grants(requested))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool Satisfies(ClaimsPrincipal principal, AuthorizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.Permissions.Count == 0)
        {
            return true;
        }

        return policy.RequireAll
            ? policy.Permissions.All(permission => HasPermission(principal, permission))
            : policy.Permissions.Any(permission => HasPermission(principal, permission));
    }

    private static bool TryParse(string value, out Permission permission)
    {
        try
        {
            permission = Permission.Parse(value);
            return true;
        }
        catch (FormatException)
        {
            permission = null!;
            return false;
        }
    }
}
