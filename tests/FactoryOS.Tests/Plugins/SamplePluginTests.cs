using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Sample;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Tests.Plugins;

public sealed class SamplePluginTests
{
    [Fact]
    public void Sample_plugin_is_a_plugin_with_the_expected_key()
    {
        var plugin = new SamplePlugin();

        Assert.IsAssignableFrom<IPlugin>(plugin);
        Assert.Equal("sample", plugin.Key);
    }

    [Fact]
    public void Configure_services_registers_the_greeter()
    {
        var services = new ServiceCollection();
        new SamplePlugin().ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ISampleGreeter>());
    }
}
