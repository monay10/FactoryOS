using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Domain;

namespace FactoryOS.Tests.Identity;

public sealed class AggregateTests
{
    [Fact]
    public void Role_grants_reflect_granted_permissions()
    {
        var role = Role.Create(Guid.NewGuid(), Guid.NewGuid(), "Operator");
        role.Grant(Permission.Parse("energy.*"));

        Assert.True(role.Grants(Permission.Parse("energy.read")));
        Assert.False(role.Grants(Permission.Parse("quality.read")));
    }

    [Fact]
    public void Role_revoke_removes_a_permission()
    {
        var role = Role.Create(Guid.NewGuid(), Guid.NewGuid(), "Operator");
        var permission = Permission.Parse("energy.read");
        role.Grant(permission);

        Assert.True(role.Revoke(permission));
        Assert.False(role.Grants(permission));
    }

    [Fact]
    public void User_role_assignment_is_idempotent()
    {
        var user = User.Create(Guid.NewGuid(), Guid.NewGuid(), "operator", "op@factory.test", "hash");
        var roleId = Guid.NewGuid();

        user.AssignRole(roleId);
        user.AssignRole(roleId);

        Assert.Single(user.RoleIds);
    }

    [Fact]
    public void User_deactivation_flips_the_flag()
    {
        var user = User.Create(Guid.NewGuid(), Guid.NewGuid(), "operator", "op@factory.test", "hash");

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Tenant_and_organization_creation_capture_their_data()
    {
        var tenantId = Guid.NewGuid();
        var tenant = Tenant.Create(tenantId, "tenant_001", "Demo");
        var org = Organization.Create(Guid.NewGuid(), tenantId, "Line 1");

        Assert.True(tenant.IsActive);
        Assert.Equal("tenant_001", tenant.Key);
        Assert.Equal(tenantId, org.TenantId);
        Assert.Equal("Line 1", org.Name);
    }
}
