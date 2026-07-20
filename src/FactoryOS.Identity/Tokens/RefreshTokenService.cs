using System.Security.Cryptography;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Results;
using FactoryOS.Identity.Domain;
using Microsoft.Extensions.Options;

namespace FactoryOS.Identity.Tokens;

/// <summary>Default <see cref="IRefreshTokenService"/> backed by an <see cref="IRefreshTokenStore"/>.</summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int TokenSizeBytes = 32;

    private readonly IRefreshTokenStore _store;
    private readonly IDateTimeProvider _clock;
    private readonly JwtOptions _options;

    /// <summary>Initializes a new instance of the <see cref="RefreshTokenService"/> class.</summary>
    /// <param name="store">The refresh-token store.</param>
    /// <param name="clock">The clock used for lifetimes.</param>
    /// <param name="options">The JWT options (refresh lifetime).</param>
    public RefreshTokenService(IRefreshTokenStore store, IDateTimeProvider clock, IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _clock = clock;
        _options = options.Value;
    }

    /// <inheritdoc />
    public RefreshToken Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = _clock.UtcNow;
        var token = RefreshToken.Create(
            GenerateTokenValue(),
            user.Id,
            user.TenantId,
            now,
            now.AddDays(_options.RefreshTokenDays));

        _store.Add(token);
        return token;
    }

    /// <inheritdoc />
    public Result<RefreshToken> Validate(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var stored = _store.Find(token);
        if (stored is null)
        {
            return Result.Failure<RefreshToken>(
                Error.NotFound("Identity.RefreshToken.NotFound", "The refresh token was not found."));
        }

        if (!stored.IsActive(_clock.UtcNow))
        {
            return Result.Failure<RefreshToken>(
                Error.Validation("Identity.RefreshToken.Inactive", "The refresh token is revoked or expired."));
        }

        return stored;
    }

    /// <inheritdoc />
    public Result<RefreshToken> Rotate(string token, User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var validation = Validate(token);
        if (validation.IsFailure)
        {
            return validation;
        }

        validation.Value.Revoke(_clock.UtcNow);
        return Issue(user);
    }

    /// <inheritdoc />
    public void Revoke(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _store.Find(token)?.Revoke(_clock.UtcNow);
    }

    private static string GenerateTokenValue()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenSizeBytes));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
