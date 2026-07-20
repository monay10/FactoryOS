using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Dependencies;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginDependencyResolverTests
{
    private static PluginManifest Manifest(string key, params PluginDependency[] dependencies)
    {
        return new PluginManifest
        {
            Key = key,
            Name = key,
            Version = PluginVersion.Parse("1.0.0"),
            Dependencies = dependencies,
        };
    }

    private static PluginDependency DependsOn(string key)
    {
        return new PluginDependency(key, PluginVersion.Parse("1.0.0"));
    }

    [Fact]
    public void Orders_dependencies_before_dependents()
    {
        var b = Manifest("b", DependsOn("a"));
        var a = Manifest("a");
        var c = Manifest("c", DependsOn("b"));

        var result = PluginDependencyResolver.Resolve([c, b, a]);

        Assert.True(result.IsSuccess);
        var order = result.Value.Select(manifest => manifest.Key).ToArray();
        Assert.True(Array.IndexOf(order, "a") < Array.IndexOf(order, "b"));
        Assert.True(Array.IndexOf(order, "b") < Array.IndexOf(order, "c"));
    }

    [Fact]
    public void Duplicate_keys_fail()
    {
        var result = PluginDependencyResolver.Resolve([Manifest("a"), Manifest("a")]);

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Dependency.Duplicate", result.Error.Code);
    }

    [Fact]
    public void Missing_dependency_fails()
    {
        var result = PluginDependencyResolver.Resolve([Manifest("a", DependsOn("ghost"))]);

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Dependency.Missing", result.Error.Code);
    }

    [Fact]
    public void Unsatisfied_version_fails()
    {
        var dependent = new PluginManifest
        {
            Key = "a",
            Name = "a",
            Version = PluginVersion.Parse("1.0.0"),
            Dependencies = [new PluginDependency("core", PluginVersion.Parse("2.0.0"))],
        };
        var core = new PluginManifest
        {
            Key = "core",
            Name = "core",
            Version = PluginVersion.Parse("1.0.0"),
        };

        var result = PluginDependencyResolver.Resolve([dependent, core]);

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Dependency.VersionMismatch", result.Error.Code);
    }

    [Fact]
    public void Dependency_cycle_fails()
    {
        var a = Manifest("a", DependsOn("b"));
        var b = Manifest("b", DependsOn("a"));

        var result = PluginDependencyResolver.Resolve([a, b]);

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Dependency.Cycle", result.Error.Code);
    }
}
