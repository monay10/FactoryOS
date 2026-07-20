using FactoryOS.Identity.Tokens;

namespace FactoryOS.Identity.Authentication;

/// <summary>The tokens issued by a successful authentication or refresh.</summary>
/// <param name="AccessToken">The signed access token and its expiry.</param>
/// <param name="RefreshToken">The refresh token used to obtain new access tokens.</param>
public sealed record AuthenticationResult(AccessToken AccessToken, RefreshToken RefreshToken);
