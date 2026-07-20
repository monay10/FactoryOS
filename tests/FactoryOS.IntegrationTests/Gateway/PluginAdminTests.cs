using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Hosting;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Gateway;

public sealed class PluginAdminTests
{
    [Fact]
    public void Disabling_an_active_plugin_switches_it_off_then_is_idempotent()
    {
        var host = new FakePluginHost(Module("energy", PluginState.Started));
        var admin = new PluginAdmin(host);

        var first = admin.SetEnabled("energy", enabled: false);
        Assert.Equal(PluginAdminOutcome.Changed, first.Outcome);
        Assert.Equal("Disabled", first.State);

        var again = admin.SetEnabled("energy", enabled: false);
        Assert.Equal(PluginAdminOutcome.Unchanged, again.Outcome);
    }

    [Fact]
    public void Enabling_a_disabled_plugin_returns_it_to_service()
    {
        var host = new FakePluginHost(Module("energy", PluginState.Disabled));
        var admin = new PluginAdmin(host);

        var result = admin.SetEnabled("energy", enabled: true);

        Assert.Equal(PluginAdminOutcome.Changed, result.Outcome);
        Assert.Equal("Started", result.State);
    }

    [Fact]
    public void An_unknown_plugin_is_not_found()
    {
        var admin = new PluginAdmin(new FakePluginHost(Module("energy", PluginState.Started)));

        Assert.Equal(PluginAdminOutcome.NotFound, admin.SetEnabled("ghost", enabled: false).Outcome);
    }

    [Fact]
    public void A_failed_plugin_cannot_be_toggled()
    {
        var admin = new PluginAdmin(new FakePluginHost(Module("energy", PluginState.Failed)));

        Assert.Equal(PluginAdminOutcome.Failed, admin.SetEnabled("energy", enabled: true).Outcome);
    }

    [Fact]
    public void Matching_is_case_insensitive_on_the_key()
    {
        var admin = new PluginAdmin(new FakePluginHost(Module("energy", PluginState.Started)));

        Assert.Equal(PluginAdminOutcome.Changed, admin.SetEnabled("ENERGY", enabled: false).Outcome);
    }
}
