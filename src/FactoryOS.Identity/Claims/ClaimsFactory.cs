using System.Globalization;
using System.Security.Claims;
using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Domain;

namespace FactoryOS.Identity.Claims;

/// <summary>Builds the claim set that describes an authenticated user (subject, tenant, roles, permissions).</summary>
public static class ClaimsFactory
{
    /// <summary>Creates the claims for a user together with the resolved roles and permissions.</summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="roleNames">The names of the roles assigned to the user.</param>
    /// <param name="permissions">The effective permissions granted to the user.</param>
    /// <returns>The ordered claim list.</returns>
    public static IReadOnlyList<Claim> Create(
        User user,
        IEnumerable<string> roleNames,
        IEnumerable<Permission> permissions)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roleNames);
        ArgumentNullException.ThrowIfNull(permissions);

        var claims = new List<Claim>
        {
            new(FactoryClaimTypes.Subject, user.Id.ToString()),
            new(FactoryClaimTypes.Tenant, user.TenantId.ToString()),
            new(FactoryClaimTypes.UserName, user.UserName),
            new(FactoryClaimTypes.Email, user.Email),
        };

        if (user.OrganizationId is { } organizationId)
        {
            claims.Add(new Claim(FactoryClaimTypes.Organization, organizationId.ToString()));
        }

        foreach (var roleName in roleNames)
        {
            claims.Add(new Claim(FactoryClaimTypes.Role, roleName));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim(FactoryClaimTypes.Permission, permission.Value));
        }

        return claims;
    }

    /// <summary>Extracts the tenant identifier from a principal.</summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns>The tenant identifier, or <see langword="null"/> when absent or malformed.</returns>
    public static Guid? GetTenantId(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var value = principal.FindFirst(FactoryClaimTypes.Tenant)?.Value;
        return Guid.TryParse(value, CultureInfo.InvariantCulture, out var tenantId) ? tenantId : null;
    }
}
