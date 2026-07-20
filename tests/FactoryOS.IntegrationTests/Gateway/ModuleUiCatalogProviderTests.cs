using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Ui;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Gateway;

public sealed class ModuleUiCatalogProviderTests
{
    [Fact]
    public void Aggregates_screens_only_from_active_modules_with_ui()
    {
        var host = new FakePluginHost(
            Module("energy", PluginState.Started, Screen("energy.b", 20), Screen("energy.a", 10)),
            Module("maintenance", PluginState.Started, Screen("maint.home", 5)),
            Module("disabled", PluginState.Disabled, Screen("hidden", 1)),
            Module("failed", PluginState.Failed, Screen("broken", 1)),
            Module("headless", PluginState.Started));

        var catalog = new ModuleUiCatalogProvider(host).GetCatalog();

        // Modules are ordered by key; disabled, failed and screen-less modules contribute nothing.
        Assert.Collection(
            catalog.Modules,
            module => Assert.Equal("energy", module.Key),
            module => Assert.Equal("maintenance", module.Key));
    }

    [Fact]
    public void Orders_screens_within_a_module_by_section_then_order()
    {
        var host = new FakePluginHost(
            Module(
                "energy",
                PluginState.Started,
                Screen("z", 30, section: "Alpha"),
                Screen("y", 10, section: "Beta"),
                Screen("x", 20, section: "Alpha")));

        var catalog = new ModuleUiCatalogProvider(host).GetCatalog();

        var screens = Assert.Single(catalog.Modules).Screens;
        Assert.Equal(["x", "z", "y"], screens.Select(screen => screen.Id));
    }

    [Fact]
    public void Navigation_regroups_screens_by_section_across_modules()
    {
        var host = new FakePluginHost(
            Module("oee", PluginState.Started, Screen("oee.board", 2, section: "Experience")),
            Module("dashboard", PluginState.Started, Screen("dash.ops", 1, section: "Experience")),
            Module("brain", PluginState.Started, Screen("brain.ask", 1, section: "AI")),
            Module("off", PluginState.Disabled, Screen("hidden", 1, section: "AI")));

        var nav = new ModuleUiCatalogProvider(host).GetNavigation();

        // Sections ordered by name (Ordinal); disabled modules contribute nothing.
        Assert.Equal(["AI", "Experience"], nav.Sections.Select(section => section.Section));

        var experience = nav.Sections.Single(section => section.Section == "Experience").Items;
        // Interleaved across modules by order, each item carrying its owning module key.
        Assert.Equal([("dashboard", "dash.ops"), ("oee", "oee.board")], experience.Select(item => (item.Module, item.Id)));
    }

    [Fact]
    public void Navigation_groups_screens_with_no_section_under_an_empty_name_that_sorts_first()
    {
        var host = new FakePluginHost(
            Module("energy", PluginState.Started, Screen("energy.home", 1, section: "Ops")),
            Module("misc", PluginState.Started, Screen("misc.loose", 1, section: null!)));

        var nav = new ModuleUiCatalogProvider(host).GetNavigation();

        Assert.Equal(["", "Ops"], nav.Sections.Select(section => section.Section));
        Assert.Equal("misc.loose", Assert.Single(nav.Sections[0].Items).Id);
    }

    [Fact]
    public void Reports_module_metadata()
    {
        var host = new FakePluginHost(Module("energy", PluginState.Started, Screen("energy.home", 1)));

        var module = Assert.Single(new ModuleUiCatalogProvider(host).GetCatalog().Modules);

        Assert.Equal("energy", module.Key);
        Assert.Equal("energy Module", module.Name);
        Assert.Equal("1.0.0", module.Version);
    }
}
