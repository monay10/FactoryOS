using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Domain;

namespace FactoryOS.Tests.Identity;

public sealed class ClaimsFactoryTests
{
    [Fact]
    public void Creates_subject_tenant_role_and_permission_claims()
    {
        var tenantId = Guid.NewGuid();
        var user = User.Create(Guid.NewGuid(), tenantId, "operator", "op@factory.test", "hash");

        var claims = ClaimsFactory.Create(
            user,
            ["Operator"],
            [Permission.Parse("energy.read")]);

        Assert.Contains(claims, c => c.Type == FactoryClaimTypes.Subject && c.Value == user.Id.ToString());
        Assert.Contains(claims, c => c.Type == FactoryClaimTypes.Tenant && c.Value == tenantId.ToString());
        Assert.Contains(claims, c => c.Type == FactoryClaimTypes.Role && c.Value == "Operator");
        Assert.Contains(claims, c => c.Type == FactoryClaimTypes.Permission && c.Value == "energy.read");
    }

    [Fact]
    public void Omits_organization_claim_when_absent()
    {
        var user = User.Create(Guid.NewGuid(), Guid.NewGuid(), "operator", "op@factory.test", "hash");

        var claims = ClaimsFactory.Create(user, [], []);

        Assert.DoesNotContain(claims, c => c.Type == FactoryClaimTypes.Organization);
    }
}
