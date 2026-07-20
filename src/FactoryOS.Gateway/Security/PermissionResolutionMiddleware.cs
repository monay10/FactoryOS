using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace FactoryOS.Gateway.Security;

/// <summary>
/// Resolves the caller's permissions once, at the edge, into the request-scoped <see cref="IPermissionContext"/>.
/// An authenticated principal wins: its permission claims (the ones the FactoryOS Identity layer issues) bind the
/// request, so a validated access token drives navigation. Otherwise the configured header is read (a comma- or
/// space-separated list) as a fallback for tools and tests. When neither is present the context is left unrestricted;
/// when either is present — even empty — the request is bound to exactly that set, so an authenticated session with no
/// permissions sees only permission-free screens. The middleware never rejects a request; whether a permission is
/// required is the screen's declaration, applied when navigation is built.
/// </summary>
public sealed class PermissionResolutionMiddleware
{
    private static readonly char[] Separators = [',', ' '];

    private readonly RequestDelegate _next;
    private readonly PermissionResolutionOptions _options;

    /// <summary>Initializes the middleware.</summary>
    /// <param name="next">The next delegate in the request pipeline.</param>
    /// <param name="options">The permission-resolution configuration.</param>
    public PermissionResolutionMiddleware(RequestDelegate next, PermissionResolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);
        _next = next;
        _options = options;
    }

    /// <summary>Resolves permissions into the scoped context, then invokes the rest of the pipeline.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="permissions">The request-scoped permission context to populate.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context, IPermissionContext permissions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(permissions);

        if (permissions is PermissionContext writable)
        {
            // An authenticated principal's permission claims win over the header fallback.
            if (context.User.Identity?.IsAuthenticated == true)
            {
                writable.Set(context.User.FindAll(_options.PermissionClaimType).Select(claim => claim.Value));
            }
            else if (context.Request.Headers.TryGetValue(_options.HeaderName, out var header))
            {
                writable.Set(header
                    .ToString()
                    .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}
