using FactoryOS.Identity.Authentication;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Credentials;
using FactoryOS.Identity.Persistence;
using FactoryOS.Identity.Seeding;
using FactoryOS.Identity.Tokens;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Identity;

public sealed class DefaultIdentitySeederTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 20, 12, 00, 00, TimeSpan.Zero);

    private sealed class Harness
    {
        public Harness()
        {
            var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                SigningKey = "0123456789-abcdefghij-ABCDEFGHIJ-key",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 7,
            });
            var clock = new MutableClock(Now);
            var hasher = new Pbkdf2PasswordHasher();
            Users = new InMemoryUserStore();
            Roles = new InMemoryRoleStore();
            AccessTokens = new JwtAccessTokenService(options, clock);
            Authenticator = new Authenticator(
                Users, Roles, hasher, AccessTokens, new RefreshTokenService(new InMemoryRefreshTokenStore(), clock, options));
            Seeder = new DefaultIdentitySeeder(Users, Roles, hasher);
        }

        public InMemoryUserStore Users { get; }
        public InMemoryRoleStore Roles { get; }
        public JwtAccessTokenService AccessTokens { get; }
        public Authenticator Authenticator { get; }
        public DefaultIdentitySeeder Seeder { get; }

        public string[] PermissionsFor(string userName, string password)
        {
            var login = Authenticator.Authenticate(IdentitySeedOptions.DefaultTenantId, userName, password);
            Assert.True(login.IsSuccess);
            var principal = AccessTokens.Validate(login.Value.AccessToken.Value);
            Assert.True(principal.IsSuccess);
            return principal.Value.FindAll(FactoryClaimTypes.Permission).Select(c => c.Value).ToArray();
        }
    }

    [Fact]
    public void Seeding_creates_the_default_roles_and_a_user_per_role()
    {
        var harness = new Harness();

        var result = harness.Seeder.Seed(new IdentitySeedOptions { Password = "Passw0rd!" });

        Assert.Equal(4, result.Roles);
        Assert.Equal(4, result.Users);
    }

    [Fact]
    public void A_seeded_admin_token_carries_the_wildcard_permission()
    {
        var harness = new Harness();
        harness.Seeder.Seed(new IdentitySeedOptions { Password = "Passw0rd!" });

        Assert.Contains("*", harness.PermissionsFor("admin", "Passw0rd!"));
    }

    [Fact]
    public void A_seeded_energy_operator_token_carries_the_energy_wildcard()
    {
        var harness = new Harness();
        harness.Seeder.Seed(new IdentitySeedOptions { Password = "Passw0rd!" });

        var permissions = harness.PermissionsFor("energy", "Passw0rd!");
        Assert.Contains("energy.*", permissions);
        Assert.Contains("dashboard.view", permissions);
        Assert.DoesNotContain("quality.view", permissions);
    }

    [Fact]
    public void Without_a_password_roles_are_seeded_but_no_users_are_invented()
    {
        var harness = new Harness();

        var result = harness.Seeder.Seed(new IdentitySeedOptions { Password = null });

        Assert.Equal(4, result.Roles);
        Assert.Equal(0, result.Users);
    }
}
