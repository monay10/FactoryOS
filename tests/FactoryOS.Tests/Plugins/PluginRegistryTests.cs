using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Registry;
using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginRegistryTests
{
    private static PluginDescriptor Descriptor(string key)
    {
        return new PluginDescriptor(new PluginManifest
        {
            Key = key,
            Name = key,
            Version = PluginVersion.Parse("1.0.0"),
        });
    }

    [Fact]
    public void Register_then_find_returns_the_descriptor()
    {
        var registry = new PluginRegistry();
        var descriptor = Descriptor("a");

        registry.Register(descriptor);

        Assert.Same(descriptor, registry.Find("a"));
        Assert.Single(registry.All);
    }

    [Fact]
    public void Find_is_case_insensitive()
    {
        var registry = new PluginRegistry();
        registry.Register(Descriptor("Energy"));

        Assert.NotNull(registry.Find("energy"));
    }

    [Fact]
    public void Disable_then_enable_restores_discovered_state()
    {
        var registry = new PluginRegistry();
        registry.Register(Descriptor("a"));

        Assert.True(registry.Disable("a"));
        Assert.Equal(PluginState.Disabled, registry.Find("a")!.State);

        Assert.True(registry.Enable("a"));
        Assert.Equal(PluginState.Discovered, registry.Find("a")!.State);
    }

    [Fact]
    public void Enable_and_disable_report_missing_keys()
    {
        var registry = new PluginRegistry();

        Assert.False(registry.Enable("ghost"));
        Assert.False(registry.Disable("ghost"));
    }
}
