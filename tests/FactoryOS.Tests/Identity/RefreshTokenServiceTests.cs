using FactoryOS.Identity.Domain;
using FactoryOS.Identity.Tokens;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Identity;

public sealed class RefreshTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 19, 12, 00, 00, TimeSpan.Zero);

    private static User NewUser() =>
        User.Create(Guid.NewGuid(), Guid.NewGuid(), "operator", "op@factory.test", "hash");

    private static (RefreshTokenService Service, MutableClock Clock) Build()
    {
        var clock = new MutableClock(Now);
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions { RefreshTokenDays = 7 });
        return (new RefreshTokenService(new InMemoryRefreshTokenStore(), clock, options), clock);
    }

    [Fact]
    public void Issued_token_validates()
    {
        var (service, _) = Build();
        var user = NewUser();

        var issued = service.Issue(user);

        Assert.True(service.Validate(issued.Token).IsSuccess);
        Assert.Equal(user.Id, issued.UserId);
    }

    [Fact]
    public void Expired_token_is_inactive()
    {
        var (service, clock) = Build();
        var issued = service.Issue(NewUser());

        clock.Advance(TimeSpan.FromDays(8));

        var result = service.Validate(issued.Token);
        Assert.True(result.IsFailure);
        Assert.Equal("Identity.RefreshToken.Inactive", result.Error.Code);
    }

    [Fact]
    public void Revoked_token_fails_validation()
    {
        var (service, _) = Build();
        var issued = service.Issue(NewUser());

        service.Revoke(issued.Token);

        Assert.True(service.Validate(issued.Token).IsFailure);
    }

    [Fact]
    public void Rotation_revokes_the_old_token_and_issues_a_new_one()
    {
        var (service, _) = Build();
        var user = NewUser();
        var original = service.Issue(user);

        var rotated = service.Rotate(original.Token, user);

        Assert.True(rotated.IsSuccess);
        Assert.NotEqual(original.Token, rotated.Value.Token);
        Assert.True(service.Validate(original.Token).IsFailure);
        Assert.True(service.Validate(rotated.Value.Token).IsSuccess);
    }

    [Fact]
    public void Unknown_token_is_not_found()
    {
        var (service, _) = Build();

        var result = service.Validate("does-not-exist");

        Assert.Equal("Identity.RefreshToken.NotFound", result.Error.Code);
    }
}
