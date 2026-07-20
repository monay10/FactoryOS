using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Gateway.Security;

/// <summary>
/// An endpoint filter that short-circuits with <c>403 Forbidden</c> when the request does not hold a required
/// permission. It is the write-side counterpart to navigation filtering: hiding a screen keeps an unentitled caller
/// from seeing an action, but only this filter <i>authorizes</i> the action itself, so a hand-crafted request cannot
/// perform it. Consistent with additive RBAC, an unrestricted request (no permission set resolved) is allowed —
/// <see cref="IPermissionContext.Holds"/> returns <see langword="true"/> — so the guard narrows only when a real
/// permission set is presented.
/// </summary>
public sealed class RequirePermissionEndpointFilter : IEndpointFilter
{
    private readonly string _permission;

    /// <summary>Initializes the filter for a specific required permission.</summary>
    /// <param name="permission">The permission key the request must hold.</param>
    public RequirePermissionEndpointFilter(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        _permission = permission;
    }

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var permissions = context.HttpContext.RequestServices.GetRequiredService<IPermissionContext>();
        if (!permissions.Holds(_permission))
        {
            return Results.Json(
                new { error = $"The '{_permission}' permission is required." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context).ConfigureAwait(false);
    }
}
