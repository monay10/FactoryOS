using FactoryOS.Identity.Authentication;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Api;

/// <summary>
/// The credential and session endpoints exposed by the API host. <c>POST /auth/login</c> exchanges credentials for a
/// signed access token plus a refresh token; <c>POST /auth/refresh</c> rotates a valid refresh token into a new pair
/// so the SPA can renew a short-lived access token without prompting for credentials again. Both return the effective
/// permissions carried by the access token, so the client holds exactly what the gateway will enforce. Mapping lives
/// in one place so the host and its integration tests wire the identical behaviour.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Maps <c>/auth/login</c> and <c>/auth/refresh</c> onto the endpoint route builder.</summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <returns>The same <paramref name="endpoints"/> instance, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Exchanges credentials for a signed access token via the Identity layer. The token carries the user's
        // permission claims; the SPA sends it as a Bearer token and the gateway filters navigation by those claims.
        endpoints.MapPost("/auth/login", (LoginRequest request, IAuthenticator authenticator, IAccessTokenService tokens) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { error = "userName and password are required." });
            }

            var result = authenticator.Authenticate(request.TenantId, request.UserName, request.Password);
            return result.IsFailure
                ? Results.Unauthorized()
                : Results.Ok(ToResponse(result.Value, tokens));
        });

        // Rotates a still-active refresh token into a fresh access/refresh pair. Rotation revokes the presented
        // token, so a replayed or expired token is rejected — the client must always use the newest refresh token.
        endpoints.MapPost("/auth/refresh", (RefreshRequest request, IAuthenticator authenticator, IAccessTokenService tokens) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Results.BadRequest(new { error = "refreshToken is required." });
            }

            var result = authenticator.Refresh(request.RefreshToken);
            return result.IsFailure
                ? Results.Unauthorized()
                : Results.Ok(ToResponse(result.Value, tokens));
        });

        return endpoints;
    }

    /// <summary>Projects an authentication result into the wire response, reading the token's permission claims.</summary>
    private static LoginResponse ToResponse(AuthenticationResult result, IAccessTokenService tokens)
    {
        var access = result.AccessToken;
        var permissions = tokens.Validate(access.Value) is { IsSuccess: true } principal
            ? principal.Value.FindAll(FactoryClaimTypes.Permission).Select(claim => claim.Value).ToArray()
            : [];

        return new LoginResponse(
            access.Value,
            access.ExpiresOnUtc,
            result.RefreshToken.Token,
            result.RefreshToken.ExpiresOnUtc,
            permissions);
    }
}

/// <summary>A credentials login request.</summary>
/// <param name="TenantId">The tenant the user belongs to.</param>
/// <param name="UserName">The user name.</param>
/// <param name="Password">The plaintext password.</param>
internal sealed record LoginRequest(Guid TenantId, string UserName, string Password);

/// <summary>A request to rotate a refresh token into a fresh access/refresh pair.</summary>
/// <param name="RefreshToken">The refresh token value previously issued to the client.</param>
internal sealed record RefreshRequest(string RefreshToken);

/// <summary>The result of a successful login or refresh.</summary>
/// <param name="AccessToken">The signed JWT access token.</param>
/// <param name="ExpiresAt">The access token's UTC expiry.</param>
/// <param name="RefreshToken">The refresh token the client stores to renew the access token.</param>
/// <param name="RefreshTokenExpiresAt">The refresh token's UTC expiry.</param>
/// <param name="Permissions">The effective permissions carried by the access token, for the client to hold.</param>
internal sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    IReadOnlyList<string> Permissions);
