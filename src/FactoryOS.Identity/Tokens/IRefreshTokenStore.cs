namespace FactoryOS.Identity.Tokens;

/// <summary>Persists refresh tokens.</summary>
public interface IRefreshTokenStore
{
    /// <summary>Adds a refresh token.</summary>
    /// <param name="refreshToken">The token to store.</param>
    void Add(RefreshToken refreshToken);

    /// <summary>Finds a refresh token by its value.</summary>
    /// <param name="token">The token value.</param>
    /// <returns>The token, or <see langword="null"/> when not found.</returns>
    RefreshToken? Find(string token);
}
