using FactoryOS.Configuration.Model;

namespace FactoryOS.Tests.Configuration;

public sealed class TenantConfigurationTests
{
    private static TenantConfiguration Build() => new()
    {
        TenantId = "tenant_001",
        Name = "Demo",
        Modules =
        [
            new ModuleConfiguration { Key = "energy", Enabled = true },
            new ModuleConfiguration { Key = "quality", Enabled = false },
        ],
        Plugins =
        [
            new PluginConfiguration { Key = "logo", Enabled = true },
        ],
    };

    [Fact]
    public void GetModule_is_case_insensitive()
    {
        Assert.NotNull(Build().GetModule("ENERGY"));
    }

    [Fact]
    public void IsModuleEnabled_reflects_the_enabled_flag()
    {
        var config = Build();

        Assert.True(config.IsModuleEnabled("energy"));
        Assert.False(config.IsModuleEnabled("quality"));
        Assert.False(config.IsModuleEnabled("absent"));
    }

    [Fact]
    public void IsPluginEnabled_finds_enabled_plugins()
    {
        var config = Build();

        Assert.True(config.IsPluginEnabled("logo"));
        Assert.False(config.IsPluginEnabled("sap"));
    }
}
