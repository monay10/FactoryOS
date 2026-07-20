using FactoryOS.Identity.Tokens;

namespace FactoryOS.Api;

/// <summary>
/// The edge bridge from the Identity layer to the gateway: it validates a <c>Bearer</c> access token with the
/// Identity <see cref="IAccessTokenService"/> and, on success, publishes the resulting principal onto the request so
/// downstream resolution (tenant, permissions) reads a real, signed identity. An absent or invalid token is not an
/// error — the request simply continues unauthenticated, and whether authentication is required is each endpoint's
/// decision. This keeps the gateway free of any Identity reference; it only reads the standard claims principal.
/// </summary>
public sealed class BearerAuthenticationMiddleware
{
    private const string Scheme = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly IAccessTokenService _tokens;

    /// <summary>Initializes the middleware.</summary>
    /// <param name="next">The next delegate in the request pipeline.</param>
    /// <param name="tokens">The Identity access-token service used to validate the bearer token.</param>
    public BearerAuthenticationMiddleware(RequestDelegate next, IAccessTokenService tokens)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(tokens);
        _next = next;
        _tokens = tokens;
    }

    /// <summary>Validates a bearer token into the request principal, then invokes the rest of the pipeline.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith(Scheme, StringComparison.Ordinal))
        {
            var validated = _tokens.Validate(header[Scheme.Length..].Trim());
            if (validated.IsSuccess)
            {
                context.User = validated.Value;
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}
