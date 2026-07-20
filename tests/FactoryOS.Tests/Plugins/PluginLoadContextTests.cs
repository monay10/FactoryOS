using FactoryOS.Plugin.Isolation;
using FactoryOS.Plugins.Sample;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginLoadContextTests
{
    [Fact]
    public void Loads_the_plugin_assembly_into_an_isolated_collectible_context()
    {
        var assemblyPath = typeof(SamplePlugin).Assembly.Location;

        var context = new PluginLoadContext(assemblyPath);
        var isolated = context.LoadFromAssemblyPath(assemblyPath);

        Assert.True(context.IsCollectible);
        Assert.StartsWith("PluginLoadContext:", context.Name);
        Assert.NotNull(isolated);

        // The same assembly loaded through the isolated context is a distinct instance from the one
        // already loaded into the default context — proving isolation.
        Assert.NotSame(typeof(SamplePlugin).Assembly, isolated);
    }
}
