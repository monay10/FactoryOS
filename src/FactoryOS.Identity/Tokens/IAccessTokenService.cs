using System.Security.Claims;
using FactoryOS.Domain.Results;

namespace FactoryOS.Identity.Tokens;

/// <summary>Issues and validates signed JWT access tokens.</summary>
public interface IAccessTokenService
{
    /// <summary>Creates a signed access token carrying the supplied claims.</summary>
    /// <param name="claims">The claims to embed.</param>
    /// <returns>The issued token and its UTC expiry.</returns>
    AccessToken Create(IEnumerable<Claim> claims);

    /// <summary>Validates a token's signature, issuer, audience and lifetime.</summary>
    /// <param name="token">The token to validate.</param>
    /// <returns>A successful result with the principal, or a failure describing why it is invalid.</returns>
    Result<ClaimsPrincipal> Validate(string token);
}
