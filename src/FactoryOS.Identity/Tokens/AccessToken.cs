namespace FactoryOS.Identity.Tokens;

/// <summary>An issued access token and the instant it expires.</summary>
/// <param name="Value">The encoded JWT.</param>
/// <param name="ExpiresOnUtc">The UTC expiry instant.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresOnUtc);
