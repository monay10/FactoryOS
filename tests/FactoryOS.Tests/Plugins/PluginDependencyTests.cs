using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginDependencyTests
{
    [Fact]
    public void Equal_or_higher_version_satisfies_the_dependency()
    {
        var dependency = new PluginDependency("core", PluginVersion.Parse("1.2.0"));

        Assert.True(dependency.IsSatisfiedBy(PluginVersion.Parse("1.2.0")));
        Assert.True(dependency.IsSatisfiedBy(PluginVersion.Parse("1.3.0")));
    }

    [Fact]
    public void Lower_version_does_not_satisfy_the_dependency()
    {
        var dependency = new PluginDependency("core", PluginVersion.Parse("1.2.0"));

        Assert.False(dependency.IsSatisfiedBy(PluginVersion.Parse("1.1.9")));
    }
}
