using FactoryOS.Infrastructure.Configuration;
using FactoryOS.Infrastructure.Execution;
using FactoryOS.Shared.Identifiers;

namespace FactoryOS.Tests.Infrastructure;

public sealed class CurrentContextTests
{
    [Fact]
    public void An_uninitialized_context_is_anonymous_with_no_scope()
    {
        var context = new InfrastructureContext();

        Assert.False(new CurrentUser(context).IsAuthenticated);
        Assert.Empty(new CurrentUser(context).Permissions);
        Assert.False(new CurrentTenant(context).HasTenant);
        Assert.Null(new CurrentFactory(context).FactoryId);
        Assert.Null(new CurrentPlant(context).PlantId);
        Assert.Null(new CurrentWorkCenter(context).WorkCenterId);
    }

    [Fact]
    public void Initialize_populates_the_caller_and_scope()
    {
        var context = new InfrastructureContext();
        var userId = UserId.New();
        var factoryId = FactoryId.New();
        var plantId = PlantId.New();
        var workCenterId = WorkCenterId.New();

        context.Initialize("acme", userId, "tester", ["dashboard.view"], factoryId, plantId, workCenterId);

        Assert.True(new CurrentUser(context).IsAuthenticated);
        Assert.Equal(userId, new CurrentUser(context).UserId);
        Assert.Equal("tester", new CurrentUser(context).UserName);
        Assert.Equal("acme", new CurrentTenant(context).Tenant);
        Assert.True(new CurrentTenant(context).HasTenant);
        Assert.Equal(factoryId, new CurrentFactory(context).FactoryId);
        Assert.Equal(plantId, new CurrentPlant(context).PlantId);
        Assert.Equal(workCenterId, new CurrentWorkCenter(context).WorkCenterId);
    }

    [Theory]
    [InlineData(new[] { "dashboard.view" }, "dashboard.view", true)]
    [InlineData(new[] { "dashboard.view" }, "maintenance.close", false)]
    [InlineData(new[] { "*" }, "anything.at.all", true)]
    [InlineData(new[] { "maintenance.*" }, "maintenance.close", true)]
    [InlineData(new[] { "maintenance.*" }, "quality.view", false)]
    [InlineData(new[] { "DASHBOARD.VIEW" }, "dashboard.view", true)]
    public void HasPermission_honors_the_wildcard_convention(string[] grants, string required, bool expected)
    {
        var context = new InfrastructureContext();
        context.Initialize(tenant: "acme", userId: UserId.New(), userName: "tester", permissions: grants);

        Assert.Equal(expected, new CurrentUser(context).HasPermission(required));
    }

    [Fact]
    public void An_anonymous_caller_holds_no_permission()
    {
        var context = new InfrastructureContext();

        Assert.False(new CurrentUser(context).HasPermission("dashboard.view"));
    }
}
