using System.Security.Claims;
using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Claims;

namespace FactoryOS.Tests.Identity;

public sealed class PermissionAuthorizerTests
{
    private static readonly PermissionAuthorizer Authorizer = new();

    private static ClaimsPrincipal Principal(params string[] permissions)
    {
        var claims = permissions.Select(p => new Claim(FactoryClaimTypes.Permission, p));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void Grants_exact_permission()
    {
        Assert.True(Authorizer.HasPermission(Principal("energy.read"), "energy.read"));
    }

    [Fact]
    public void Wildcard_claim_grants_specific_permission()
    {
        Assert.True(Authorizer.HasPermission(Principal("energy.*"), "energy.write"));
        Assert.True(Authorizer.HasPermission(Principal("*"), "anything.goes"));
    }

    [Fact]
    public void Missing_permission_is_denied()
    {
        Assert.False(Authorizer.HasPermission(Principal("energy.read"), "quality.read"));
    }

    [Fact]
    public void Require_all_policy_needs_every_permission()
    {
        var policy = AuthorizationPolicy.RequireAllOf("p", "energy.read", "energy.write");

        Assert.True(Authorizer.Satisfies(Principal("energy.*"), policy));
        Assert.False(Authorizer.Satisfies(Principal("energy.read"), policy));
    }

    [Fact]
    public void Require_any_policy_needs_one_permission()
    {
        var policy = AuthorizationPolicy.RequireAnyOf("p", "energy.read", "quality.read");

        Assert.True(Authorizer.Satisfies(Principal("quality.read"), policy));
        Assert.False(Authorizer.Satisfies(Principal("maintenance.read"), policy));
    }
}
