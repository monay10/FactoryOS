using FactoryOS.Gateway.Security;

namespace FactoryOS.IntegrationTests.Gateway;

public sealed class PermissionMatchTests
{
    [Theory]
    [InlineData("*", "energy.view", true)]                 // super-admin grants everything
    [InlineData("energy.*", "energy.view", true)]          // resource wildcard grants any action
    [InlineData("energy.*", "energy.export", true)]
    [InlineData("energy.view", "energy.view", true)]       // exact
    [InlineData("ENERGY.VIEW", "energy.view", true)]       // case-insensitive
    [InlineData("energy.*", "quality.view", false)]        // different resource
    [InlineData("energy.view", "energy.export", false)]    // different action, no wildcard
    [InlineData("energy", "energy.view", false)]           // malformed grant is not a wildcard
    [InlineData("energyx.*", "energy.view", false)]        // prefix must match on the dot boundary
    public void Grants_matches_the_resource_action_wildcard_convention(string grant, string required, bool expected)
    {
        Assert.Equal(expected, PermissionMatch.Grants(grant, required));
    }
}
