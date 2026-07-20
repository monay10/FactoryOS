using System.Security.Claims;

namespace FactoryOS.Identity.Authorization;

/// <summary>Evaluates a principal's permission claims against required permissions and policies.</summary>
public interface IPermissionAuthorizer
{
    /// <summary>Determines whether a principal holds a permission (honoring wildcard grants).</summary>
    /// <param name="principal">The principal to check.</param>
    /// <param name="permission">The required permission (<c>resource.action</c>).</param>
    /// <returns><see langword="true"/> when the principal is granted the permission.</returns>
    bool HasPermission(ClaimsPrincipal principal, string permission);

    /// <summary>Determines whether a principal satisfies an authorization policy.</summary>
    /// <param name="principal">The principal to check.</param>
    /// <param name="policy">The policy to evaluate.</param>
    /// <returns><see langword="true"/> when the principal satisfies the policy.</returns>
    bool Satisfies(ClaimsPrincipal principal, AuthorizationPolicy policy);
}
