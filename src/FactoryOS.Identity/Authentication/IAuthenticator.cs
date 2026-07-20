using FactoryOS.Domain.Results;

namespace FactoryOS.Identity.Authentication;

/// <summary>Authenticates credentials and exchanges refresh tokens for fresh tokens.</summary>
public interface IAuthenticator
{
    /// <summary>Authenticates a user by tenant, user name and password.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The plaintext password.</param>
    /// <returns>A successful result with the issued tokens, or a failure.</returns>
    Result<AuthenticationResult> Authenticate(Guid tenantId, string userName, string password);

    /// <summary>Exchanges a valid refresh token for a new access and refresh token (rotation).</summary>
    /// <param name="refreshToken">The refresh token value.</param>
    /// <returns>A successful result with the new tokens, or a failure.</returns>
    Result<AuthenticationResult> Refresh(string refreshToken);
}
