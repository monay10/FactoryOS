using FactoryOS.Identity.Authentication;
using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Credentials;
using FactoryOS.Identity.Domain;
using FactoryOS.Identity.Persistence;
using FactoryOS.Identity.Tokens;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Identity;

public sealed class AuthenticatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class Harness
    {
        public Harness()
        {
            var clock = new MutableClock(Now);
            var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                SigningKey = "0123456789-abcdefghij-ABCDEFGHIJ-key",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 7,
            });

            Hasher = new Pbkdf2PasswordHasher();
            Users = new InMemoryUserStore();
            Roles = new InMemoryRoleStore();
            AccessTokens = new JwtAccessTokenService(options, clock);
            var refreshTokens = new RefreshTokenService(new InMemoryRefreshTokenStore(), clock, options);
            Authenticator = new Authenticator(Users, Roles, Hasher, AccessTokens, refreshTokens);
        }

        public IPasswordHasher Hasher { get; }

        public InMemoryUserStore Users { get; }

        public InMemoryRoleStore Roles { get; }

        public JwtAccessTokenService AccessTokens { get; }

        public Authenticator Authenticator { get; }

        public User SeedUser(string password, bool active = true, params Permission[] permissions)
        {
            var role = Role.Create(Guid.NewGuid(), TenantId, "Operator");
            foreach (var permission in permissions)
            {
                role.Grant(permission);
            }

            Roles.Add(role);

            var user = User.Create(Guid.NewGuid(), TenantId, "operator", "op@factory.test", Hasher.Hash(password));
            user.AssignRole(role.Id);
            if (!active)
            {
                user.Deactivate();
            }

            Users.Add(user);
            return user;
        }
    }

    [Fact]
    public void Successful_authentication_issues_tokens_with_permission_claims()
    {
        var harness = new Harness();
        harness.SeedUser("Passw0rd!", permissions: Permission.Parse("energy.read"));

        var result = harness.Authenticator.Authenticate(TenantId, "operator", "Passw0rd!");

        Assert.True(result.IsSuccess);
        var principal = harness.AccessTokens.Validate(result.Value.AccessToken.Value);
        Assert.True(principal.IsSuccess);
        Assert.Equal(TenantId, ClaimsFactory.GetTenantId(principal.Value));
        Assert.Contains(
            principal.Value.FindAll(FactoryClaimTypes.Permission),
            claim => claim.Value == "energy.read");
    }

    [Fact]
    public void Wrong_password_is_rejected()
    {
        var harness = new Harness();
        harness.SeedUser("Passw0rd!");

        var result = harness.Authenticator.Authenticate(TenantId, "operator", "nope");

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.Auth.InvalidCredentials", result.Error.Code);
    }

    [Fact]
    public void Unknown_user_is_rejected_indistinguishably()
    {
        var harness = new Harness();

        var result = harness.Authenticator.Authenticate(TenantId, "ghost", "whatever");

        Assert.Equal("Identity.Auth.InvalidCredentials", result.Error.Code);
    }

    [Fact]
    public void Inactive_user_cannot_authenticate()
    {
        var harness = new Harness();
        harness.SeedUser("Passw0rd!", active: false);

        var result = harness.Authenticator.Authenticate(TenantId, "operator", "Passw0rd!");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Refresh_rotates_tokens_for_a_valid_refresh_token()
    {
        var harness = new Harness();
        harness.SeedUser("Passw0rd!", permissions: Permission.Parse("energy.read"));
        var login = harness.Authenticator.Authenticate(TenantId, "operator", "Passw0rd!");

        var refreshed = harness.Authenticator.Refresh(login.Value.RefreshToken.Token);

        Assert.True(refreshed.IsSuccess);
        Assert.NotEqual(login.Value.RefreshToken.Token, refreshed.Value.RefreshToken.Token);
        Assert.True(harness.AccessTokens.Validate(refreshed.Value.AccessToken.Value).IsSuccess);
    }
}
