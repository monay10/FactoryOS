using FactoryOS.Gateway.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Convenience registration for the tenant-required endpoint filter.</summary>
public static class RequireTenantRouteHandlerBuilderExtensions
{
    /// <summary>
    /// Requires a resolved tenant on the route: requests without one are rejected with <c>400 Bad Request</c>
    /// before the handler runs, so the handler may read <c>ITenantContext.Tenant</c> unconditionally.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The same <paramref name="builder"/> instance, to allow chaining.</returns>
    public static RouteHandlerBuilder RequireTenant(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter<RequireTenantEndpointFilter>();
    }
}
