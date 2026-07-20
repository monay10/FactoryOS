using System.Security.Claims;
using FactoryOS.Domain.Results;
using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Credentials;
using FactoryOS.Identity.Domain;
using FactoryOS.Identity.Persistence;
using FactoryOS.Identity.Tokens;

namespace FactoryOS.Identity.Authentication;

/// <summary>
/// Default <see cref="IAuthenticator"/>. Verifies credentials, resolves the user's effective roles and
/// permissions, and issues an access token plus a refresh token. Credential failures are deliberately
/// indistinguishable (no user enumeration).
/// </summary>
public sealed class Authenticator : IAuthenticator
{
    private static readonly Error InvalidCredentials =
        Error.Validation("Identity.Auth.InvalidCredentials", "Invalid user name or password.");

    private readonly IUserStore _users;
    private readonly IRoleStore _roles;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenService _accessTokens;
    private readonly IRefreshTokenService _refreshTokens;

    /// <summary>Initializes a new instance of the <see cref="Authenticator"/> class.</summary>
    /// <param name="users">The user store.</param>
    /// <param name="roles">The role store.</param>
    /// <param name="passwordHasher">The password hasher.</param>
    /// <param name="accessTokens">The access-token service.</param>
    /// <param name="refreshTokens">The refresh-token service.</param>
    public Authenticator(
        IUserStore users,
        IRoleStore roles,
        IPasswordHasher passwordHasher,
        IAccessTokenService accessTokens,
        IRefreshTokenService refreshTokens)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(accessTokens);
        ArgumentNullException.ThrowIfNull(refreshTokens);

        _users = users;
        _roles = roles;
        _passwordHasher = passwordHasher;
        _accessTokens = accessTokens;
        _refreshTokens = refreshTokens;
    }

    /// <inheritdoc />
    public Result<AuthenticationResult> Authenticate(Guid tenantId, string userName, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var user = _users.FindByUserName(tenantId, userName);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthenticationResult>(InvalidCredentials);
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash))
        {
            return Result.Failure<AuthenticationResult>(InvalidCredentials);
        }

        return Issue(user);
    }

    /// <inheritdoc />
    public Result<AuthenticationResult> Refresh(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var validation = _refreshTokens.Validate(refreshToken);
        if (validation.IsFailure)
        {
            return Result.Failure<AuthenticationResult>(validation.Error);
        }

        var user = _users.FindById(validation.Value.UserId);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthenticationResult>(InvalidCredentials);
        }

        var rotated = _refreshTokens.Rotate(refreshToken, user);
        if (rotated.IsFailure)
        {
            return Result.Failure<AuthenticationResult>(rotated.Error);
        }

        var accessToken = _accessTokens.Create(BuildClaims(user));
        return new AuthenticationResult(accessToken, rotated.Value);
    }

    private Result<AuthenticationResult> Issue(User user)
    {
        var accessToken = _accessTokens.Create(BuildClaims(user));
        var refreshToken = _refreshTokens.Issue(user);
        return new AuthenticationResult(accessToken, refreshToken);
    }

    private IReadOnlyList<Claim> BuildClaims(User user)
    {
        var roles = _roles.FindByIds(user.RoleIds);
        var roleNames = roles.Select(role => role.Name);
        var permissions = roles
            .SelectMany(role => role.Permissions)
            .Distinct()
            .ToArray();

        return ClaimsFactory.Create(user, roleNames, permissions);
    }
}
