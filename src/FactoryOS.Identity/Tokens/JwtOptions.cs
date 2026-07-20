namespace FactoryOS.Identity.Tokens;

/// <summary>Strongly-typed options for JWT access tokens and refresh tokens.</summary>
public sealed class JwtOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Gets or sets the token issuer.</summary>
    public string Issuer { get; set; } = "factoryos";

    /// <summary>Gets or sets the token audience.</summary>
    public string Audience { get; set; } = "factoryos";

    /// <summary>Gets or sets the symmetric signing key (HS256). Must be at least 32 bytes.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the access-token lifetime in minutes.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Gets or sets the refresh-token lifetime in days.</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the permitted clock skew, in seconds, applied when validating a token's lifetime.
    /// Defaults to zero (no tolerance) so token expiry is exact by default.
    /// </summary>
    public int ClockSkewSeconds { get; set; }
}
