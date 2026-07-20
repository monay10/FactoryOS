using FactoryOS.Gateway.Security;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Convenience registration for the permission-required endpoint filter.</summary>
public static class RequirePermissionRouteHandlerBuilderExtensions
{
    /// <summary>
    /// Requires the caller to hold a permission on the route: requests without it are rejected with
    /// <c>403 Forbidden</c> before the handler runs. This authorizes write actions at the API boundary, so a screen's
    /// declared <c>requiredPermission</c> is enforced and not merely used to hide navigation.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="permission">The permission key the caller must hold.</param>
    /// <returns>The same <paramref name="builder"/> instance, to allow chaining.</returns>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter(new RequirePermissionEndpointFilter(permission));
    }
}
