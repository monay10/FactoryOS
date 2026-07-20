using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Results;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FactoryOS.Identity.Tokens;

/// <summary>
/// Default <see cref="IAccessTokenService"/> using HMAC-SHA256 signed JWTs. Token lifetimes are driven
/// by the injected <see cref="IDateTimeProvider"/> so issuance and expiry are deterministic and testable.
/// </summary>
public sealed class JwtAccessTokenService : IAccessTokenService
{
    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _clock;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;
    private readonly JwtSecurityTokenHandler _handler = new();

    /// <summary>Initializes a new instance of the <see cref="JwtAccessTokenService"/> class.</summary>
    /// <param name="options">The JWT options.</param>
    /// <param name="clock">The clock used for token lifetimes.</param>
    /// <exception cref="InvalidOperationException">Thrown when the signing key is too short.</exception>
    public JwtAccessTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _options = options.Value;
        _clock = clock;

        if (Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException("The JWT signing key must be at least 32 bytes (256 bits).");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds),
        };
    }

    /// <inheritdoc />
    public AccessToken Create(IEnumerable<Claim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var issuedAt = _clock.UtcNow.UtcDateTime;
        var expires = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expires,
            signingCredentials: _signingCredentials);

        return new AccessToken(_handler.WriteToken(token), expires);
    }

    /// <inheritdoc />
    public Result<ClaimsPrincipal> Validate(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var parameters = _validationParameters.Clone();
        parameters.LifetimeValidator = (notBefore, expires, _, _) =>
        {
            var now = _clock.UtcNow.UtcDateTime;
            return (notBefore is null || notBefore <= now) && (expires is null || expires >= now);
        };

        try
        {
            var principal = _handler.ValidateToken(token, parameters, out _);
            return principal;
        }
        catch (SecurityTokenException exception)
        {
            return Result.Failure<ClaimsPrincipal>(
                Error.Validation("Identity.Token.Invalid", exception.Message));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<ClaimsPrincipal>(
                Error.Validation("Identity.Token.Malformed", exception.Message));
        }
    }
}
