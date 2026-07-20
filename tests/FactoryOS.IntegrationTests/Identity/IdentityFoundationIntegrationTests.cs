using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Domain;
using FactoryOS.Identity.Persistence;
using FactoryOS.Identity.Services;
using FactoryOS.Identity.Sessions;
using FactoryOS.Identity.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Identity;

/// <summary>
/// The Identity foundation composed end-to-end through <c>AddIdentityFoundation</c> against a real container:
/// a user registered through <see cref="IIdentityService"/> receives a genuine HMAC-signed JWT that validates
/// back into a principal carrying the tenant and permission claims; a real session is created, validated and
/// revoked; and a refresh token is issued, validated and rotated so the presented token can no longer be replayed.
/// </summary>
public sealed class IdentityFoundationIntegrationTests
{
    private const string Password = "Str0ng!Pass";

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "factoryos",
                ["Jwt:Audience"] = "factoryos",
                ["Jwt:SigningKey"] = "0123456789-abcdefghij-ABCDEFGHIJ-key",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Identity:PasswordPolicy:MinimumLength"] = "8",
                ["Identity:Session:IdleTimeoutMinutes"] = "30",
                ["Identity:Session:AbsoluteTimeoutHours"] = "8",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddIdentityFoundation(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static User RegisterOperator(IServiceProvider sp, Guid tenantId)
    {
        var roles = sp.GetRequiredService<IRoleStore>();
        var role = Role.Create(Guid.NewGuid(), tenantId, "Operator");
        role.Grant(FactoryOS.Identity.Authorization.Permission.Parse("energy.*"));
        roles.Add(role);

        var identity = sp.GetRequiredService<IIdentityService>();
        var result = identity.RegisterUser(tenantId, "alice", "alice@factoryos.local", Password);
        Assert.True(result.IsSuccess);
        result.Value.AssignRole(role.Id);
        return result.Value;
    }

    [Fact]
    public void A_registered_user_receives_a_jwt_that_validates_back_into_a_principal()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var tenantId = Guid.NewGuid();

        var user = RegisterOperator(sp, tenantId);
        var identity = sp.GetRequiredService<IIdentityService>();
        var tokens = sp.GetRequiredService<IAccessTokenService>();

        var accessToken = tokens.Create(identity.ResolveClaims(user));
        var validation = tokens.Validate(accessToken.Value);

        Assert.True(validation.IsSuccess);
        var principal = validation.Value;
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal(tenantId, ClaimsFactory.GetTenantId(principal));
        Assert.Contains(principal.FindAll(FactoryClaimTypes.Permission), c => c.Value == "energy.*");
    }

    [Fact]
    public void A_session_is_created_validated_and_revoked_through_the_wired_service()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var sessions = sp.GetRequiredService<ISessionService>();
        var session = sessions.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(sessions.Validate(session.Id).IsSuccess);
        Assert.True(sessions.Touch(session.Id).IsSuccess);

        sessions.Revoke(session.Id);
        Assert.True(sessions.Validate(session.Id).IsFailure);
    }

    [Fact]
    public void A_refresh_token_is_issued_validated_and_rotated_so_the_old_one_cannot_be_replayed()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var tenantId = Guid.NewGuid();

        var user = RegisterOperator(sp, tenantId);
        var refreshTokens = sp.GetRequiredService<IRefreshTokenService>();

        var issued = refreshTokens.Issue(user);
        Assert.True(refreshTokens.Validate(issued.Token).IsSuccess);

        var rotated = refreshTokens.Rotate(issued.Token, user);
        Assert.True(rotated.IsSuccess);
        Assert.NotEqual(issued.Token, rotated.Value.Token);

        // The rotated (old) token is revoked and can no longer be validated.
        Assert.True(refreshTokens.Validate(issued.Token).IsFailure);
        Assert.True(refreshTokens.Validate(rotated.Value.Token).IsSuccess);
    }
}
