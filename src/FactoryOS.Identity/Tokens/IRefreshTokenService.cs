using FactoryOS.Domain.Results;
using FactoryOS.Identity.Domain;

namespace FactoryOS.Identity.Tokens;

/// <summary>Issues, validates, rotates and revokes refresh tokens.</summary>
public interface IRefreshTokenService
{
    /// <summary>Issues a new refresh token for a user.</summary>
    /// <param name="user">The user to issue for.</param>
    /// <returns>The issued refresh token.</returns>
    RefreshToken Issue(User user);

    /// <summary>Validates a refresh token value.</summary>
    /// <param name="token">The token value.</param>
    /// <returns>A successful result with the active token, or a failure.</returns>
    Result<RefreshToken> Validate(string token);

    /// <summary>Rotates a refresh token: revokes the old one and issues a replacement.</summary>
    /// <param name="token">The token value to rotate.</param>
    /// <param name="user">The owning user.</param>
    /// <returns>A successful result with the new token, or a failure.</returns>
    Result<RefreshToken> Rotate(string token, User user);

    /// <summary>Revokes a refresh token.</summary>
    /// <param name="token">The token value to revoke.</param>
    void Revoke(string token);
}
