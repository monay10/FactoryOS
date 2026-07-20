using System.Security.Claims;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Tokens;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Identity;

public sealed class JwtAccessTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 19, 12, 00, 00, TimeSpan.Zero);

    private static JwtOptions Options() => new()
    {
        Issuer = "factoryos",
        Audience = "factoryos",
        SigningKey = "0123456789-abcdefghij-ABCDEFGHIJ-key",
        AccessTokenMinutes = 15,
    };

    private static JwtAccessTokenService Service(MutableClock clock, JwtOptions? options = null)
    {
        return new JwtAccessTokenService(Microsoft.Extensions.Options.Options.Create(options ?? Options()), clock);
    }

    private static Claim[] Claims() =>
    [
        new(FactoryClaimTypes.Subject, Guid.NewGuid().ToString()),
        new(FactoryClaimTypes.Permission, "energy.read"),
    ];

    [Fact]
    public void Create_then_validate_round_trips_claims()
    {
        var service = Service(new MutableClock(Now));

        var token = service.Create(Claims());
        var result = service.Validate(token.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal("energy.read", result.Value.FindFirst(FactoryClaimTypes.Permission)!.Value);
    }

    [Fact]
    public void Short_signing_key_is_rejected()
    {
        var options = Options();
        options.SigningKey = "too-short";

        Assert.Throws<InvalidOperationException>(() => Service(new MutableClock(Now), options));
    }

    [Fact]
    public void Expired_token_fails_validation()
    {
        var clock = new MutableClock(Now);
        var service = Service(clock);
        var token = service.Create(Claims());

        clock.Advance(TimeSpan.FromMinutes(16));
        var result = service.Validate(token.Value);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Token_signed_with_a_different_key_fails_validation()
    {
        var issued = Service(new MutableClock(Now)).Create(Claims());

        var otherOptions = Options();
        otherOptions.SigningKey = "ZZZZZZZZZZ-different-signing-key-9876";
        var otherService = Service(new MutableClock(Now), otherOptions);

        Assert.True(otherService.Validate(issued.Value).IsFailure);
    }

    [Fact]
    public void Malformed_token_fails_validation()
    {
        var result = Service(new MutableClock(Now)).Validate("not.a.jwt");

        Assert.True(result.IsFailure);
    }
}
