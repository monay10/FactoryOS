using System.Collections.Concurrent;

namespace FactoryOS.Identity.Tokens;

/// <summary>An in-memory <see cref="IRefreshTokenStore"/> for development and tests.</summary>
public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshToken> _tokens = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Add(RefreshToken refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        _tokens[refreshToken.Token] = refreshToken;
    }

    /// <inheritdoc />
    public RefreshToken? Find(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return _tokens.TryGetValue(token, out var refreshToken) ? refreshToken : null;
    }
}
