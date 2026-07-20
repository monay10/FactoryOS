using FactoryOS.Identity.Authorization;

namespace FactoryOS.Tests.Identity;

public sealed class PermissionTests
{
    [Fact]
    public void Exact_permission_grants_itself()
    {
        Assert.True(Permission.Parse("energy.read").Grants(Permission.Parse("energy.read")));
    }

    [Fact]
    public void Exact_permission_does_not_grant_a_different_action()
    {
        Assert.False(Permission.Parse("energy.read").Grants(Permission.Parse("energy.write")));
    }

    [Fact]
    public void Resource_wildcard_grants_any_action_on_that_resource()
    {
        var granted = Permission.Parse("energy.*");

        Assert.True(granted.Grants(Permission.Parse("energy.read")));
        Assert.True(granted.Grants(Permission.Parse("energy.write")));
        Assert.False(granted.Grants(Permission.Parse("quality.read")));
    }

    [Fact]
    public void Global_wildcard_grants_everything()
    {
        Assert.True(Permission.Parse("*").Grants(Permission.Parse("anything.here")));
    }

    [Theory]
    [InlineData("noaction")]
    [InlineData("a.b.c")]
    public void Invalid_permission_strings_are_rejected(string value)
    {
        Assert.Throws<FormatException>(() => Permission.Parse(value));
    }

    [Fact]
    public void Blank_permission_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Permission.Parse(""));
    }

    [Fact]
    public void Permissions_are_value_equal_and_case_insensitive()
    {
        Assert.Equal(Permission.Parse("Energy.Read"), Permission.Parse("energy.read"));
    }
}
