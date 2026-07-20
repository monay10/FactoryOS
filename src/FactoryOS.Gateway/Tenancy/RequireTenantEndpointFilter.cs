using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Gateway.Tenancy;

/// <summary>
/// An endpoint filter that short-circuits with <c>400 Bad Request</c> when no tenant was resolved for the
/// request. Apply it with <c>RequireTenant()</c> so a handler can read <see cref="ITenantContext.Tenant"/>
/// unconditionally, without repeating the presence check on every route. The gateway resolves the tenant at
/// the edge; this filter turns "the tenant is required here" into a single, declarative statement.
/// </summary>
public sealed class RequireTenantEndpointFilter : IEndpointFilter
{
    /// <summary>The response message returned when a request carries no resolvable tenant.</summary>
    public const string TenantRequiredMessage =
        "A tenant is required via the 'X-FactoryOS-Tenant' header or the 'tenant' query parameter.";

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        if (!tenant.HasTenant)
        {
            return Results.BadRequest(TenantRequiredMessage);
        }

        return await next(context).ConfigureAwait(false);
    }
}
