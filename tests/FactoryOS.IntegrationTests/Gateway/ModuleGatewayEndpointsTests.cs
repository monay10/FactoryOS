using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Branding;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Gateway;

public sealed class ModuleGatewayEndpointsTests
{
    private static Task<WebApplication> StartGatewayAsync(IPluginHost host, params IModuleApi[] apis) =>
        StartGatewayAsync(host, new TenantBrandingProvider(), apis);

    private static async Task<WebApplication> StartGatewayAsync(
        IPluginHost host,
        ITenantBrandingProvider branding,
        IModuleApi[] apis)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton(host);
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddSingleton(branding);
        builder.Services.AddPluginAdministration();
        builder.Services.AddTenantResolution();
        builder.Services.AddPermissionResolution();
        foreach (var api in apis)
        {
            builder.Services.AddSingleton(api);
        }

        var app = builder.Build();
        app.UseTenantResolution();
        app.UsePermissionResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Mounts_the_endpoints_of_an_active_module()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(Module("sample", PluginState.Started, Screen("s", 1))),
            new FakeModuleApi("sample"));

        var response = await app.GetTestClient().GetAsync(new Uri("/m/sample/ping", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("sample", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_mount_disabled_or_unknown_modules()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                Module("sample", PluginState.Started),
                Module("off", PluginState.Disabled)),
            new FakeModuleApi("sample"),
            new FakeModuleApi("off"),
            new FakeModuleApi("ghost"));

        var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/m/sample/ping", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri("/m/off/ping", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri("/m/ghost/ping", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Lists_every_known_module_with_its_state()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                Module("sample", PluginState.Started),
                Module("off", PluginState.Disabled)));

        await using var stream = await app.GetTestClient().GetStreamAsync(new Uri("/modules", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var byKey = document.RootElement.EnumerateArray()
            .ToDictionary(element => element.GetProperty("key").GetString()!, element => element.GetProperty("state").GetString());

        Assert.Equal("Started", byKey["sample"]);
        Assert.Equal("Disabled", byKey["off"]);
        Assert.Equal("/m/sample", document.RootElement.EnumerateArray()
            .First(element => element.GetProperty("key").GetString() == "sample")
            .GetProperty("routePrefix").GetString());
    }

    [Fact]
    public async Task Serves_the_api_discovery_catalog_of_active_modules_only()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                Module("silent", PluginState.Started), // active but declares no api routes
                ApiModule("activity", PluginState.Started, Route("GET", "/m/activity/feed", "tenant", "max")),
                ApiModule("off", PluginState.Disabled, Route("GET", "/m/off/x"))));

        await using var stream = await app.GetTestClient().GetStreamAsync(new Uri("/modules/api", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var module = Assert.Single(document.RootElement.EnumerateArray()); // silent + disabled excluded
        Assert.Equal("activity", module.GetProperty("key").GetString());

        var route = Assert.Single(module.GetProperty("routes").EnumerateArray());
        Assert.Equal("GET", route.GetProperty("method").GetString());
        Assert.Equal("/m/activity/feed", route.GetProperty("path").GetString());
        Assert.Equal(2, route.GetProperty("query").GetArrayLength());
    }

    [Fact]
    public async Task Reports_the_resolved_tenant_from_the_header()
    {
        await using var app = await StartGatewayAsync(new FakePluginHost(Module("sample", PluginState.Started)));

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/tenant", UriKind.Relative));
        request.Headers.Add("X-FactoryOS-Tenant", "acme");
        var response = await app.GetTestClient().SendAsync(request);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("resolved").GetBoolean());
        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
    }

    [Fact]
    public async Task Reports_no_tenant_when_none_is_supplied()
    {
        await using var app = await StartGatewayAsync(new FakePluginHost(Module("sample", PluginState.Started)));

        await using var stream = await app.GetTestClient().GetStreamAsync(new Uri("/tenant", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.False(document.RootElement.GetProperty("resolved").GetBoolean());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("tenant").ValueKind);
    }

    [Fact]
    public async Task Serves_the_ui_registry_of_active_modules()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                Module("energy", PluginState.Started, Screen("energy.home", 1)),
                Module("off", PluginState.Disabled, Screen("hidden", 1))));

        await using var stream = await app.GetTestClient().GetStreamAsync(new Uri("/modules/ui", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var modules = document.RootElement.GetProperty("modules");
        var module = Assert.Single(modules.EnumerateArray());
        Assert.Equal("energy", module.GetProperty("key").GetString());

        var screen = Assert.Single(module.GetProperty("screens").EnumerateArray());
        Assert.Equal("energy.home", screen.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Serves_the_navigation_grouped_by_section_across_modules()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                Module("oee", PluginState.Started, Screen("oee.board", 2, section: "Experience")),
                Module("dashboard", PluginState.Started, Screen("dash.ops", 1, section: "Experience")),
                Module("brain", PluginState.Started, Screen("brain.ask", 1, section: "AI")),
                Module("off", PluginState.Disabled, Screen("hidden", 1, section: "Experience"))));

        await using var stream = await app.GetTestClient().GetStreamAsync(new Uri("/modules/ui/nav", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var sections = document.RootElement.GetProperty("sections").EnumerateArray().ToArray();
        // Sections are ordered by name (Ordinal): "AI" before "Experience"; disabled modules contribute nothing.
        Assert.Equal("AI", sections[0].GetProperty("section").GetString());
        Assert.Equal("Experience", sections[1].GetProperty("section").GetString());

        var experience = sections[1].GetProperty("items").EnumerateArray().ToArray();
        // Within a section, screens from different modules interleave by order, carrying their owning module.
        Assert.Equal("dashboard", experience[0].GetProperty("module").GetString());
        Assert.Equal("dash.ops", experience[0].GetProperty("id").GetString());
        Assert.Equal("oee", experience[1].GetProperty("module").GetString());
    }

    [Fact]
    public async Task Serves_the_store_catalog_with_dependency_satisfaction()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                Module("oee", PluginState.Started),
                DependentModule(
                    "maintenance",
                    PluginState.Started,
                    Requires("oee"),   // active at 1.0.0 -> satisfied
                    Requires("off"),   // present but disabled -> not satisfied
                    Requires("ghost")),// absent -> not satisfied
                Module("off", PluginState.Disabled)));

        await using var stream = await app.GetTestClient().GetStreamAsync(new Uri("/store/plugins", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var plugins = document.RootElement.GetProperty("plugins").EnumerateArray().ToArray();
        // Every known plugin appears, ordered by key (Ordinal) — including the disabled one.
        Assert.Equal(["maintenance", "oee", "off"], plugins.Select(p => p.GetProperty("key").GetString()));
        Assert.Equal("Disabled", plugins[2].GetProperty("state").GetString());

        var deps = plugins[0].GetProperty("dependencies").EnumerateArray()
            .ToDictionary(d => d.GetProperty("pluginKey").GetString()!, d => d.GetProperty("satisfied").GetBoolean());
        Assert.True(deps["oee"]);      // active at a satisfying version
        Assert.False(deps["off"]);     // disabled plugins satisfy nothing
        Assert.False(deps["ghost"]);   // not installed
    }

    [Fact]
    public async Task Summarizes_the_store_by_state_and_unmet_dependencies()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                Module("oee", PluginState.Started),
                Module("energy", PluginState.Started),
                DependentModule("maintenance", PluginState.Started, Requires("ghost")), // unmet
                Module("off", PluginState.Disabled)));

        await using var stream = await app.GetTestClient().GetStreamAsync(new Uri("/store/summary", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(4, document.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("withUnmetDependencies").GetInt32());

        var byState = document.RootElement.GetProperty("byState").EnumerateArray().ToArray();
        // Started (3) leads Disabled (1), count descending.
        Assert.Equal("Started", byState[0].GetProperty("state").GetString());
        Assert.Equal(3, byState[0].GetProperty("count").GetInt32());
        Assert.Equal("Disabled", byState[1].GetProperty("state").GetString());
    }

    [Fact]
    public async Task Disabling_a_plugin_flips_its_state_and_dependents_dependency_health()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                Module("oee", PluginState.Started),
                DependentModule("maintenance", PluginState.Started, Requires("oee"))));

        var client = app.GetTestClient();

        // Before: maintenance's dependency on oee is satisfied.
        using (var before = JsonDocument.Parse(await client.GetStringAsync(new Uri("/store/plugins", UriKind.Relative))))
        {
            var maintenance = before.RootElement.GetProperty("plugins").EnumerateArray()
                .First(p => p.GetProperty("key").GetString() == "maintenance");
            Assert.True(maintenance.GetProperty("dependencies")[0].GetProperty("satisfied").GetBoolean());
        }

        // Disable oee.
        var disable = await client.PostAsync(new Uri("/store/plugins/oee/disable", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        using (var body = JsonDocument.Parse(await disable.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Disabled", body.RootElement.GetProperty("state").GetString());
        }

        // After: maintenance's dependency is no longer satisfied — a disabled plugin offers nothing.
        using (var after = JsonDocument.Parse(await client.GetStringAsync(new Uri("/store/plugins", UriKind.Relative))))
        {
            var maintenance = after.RootElement.GetProperty("plugins").EnumerateArray()
                .First(p => p.GetProperty("key").GetString() == "maintenance");
            Assert.False(maintenance.GetProperty("dependencies")[0].GetProperty("satisfied").GetBoolean());
        }
    }

    [Fact]
    public async Task Enabling_returns_a_plugin_to_the_active_discovery_surface()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(Module("energy", PluginState.Disabled)));

        var client = app.GetTestClient();

        var enable = await client.PostAsync(new Uri("/store/plugins/energy/enable", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        using var body = JsonDocument.Parse(await enable.Content.ReadAsStringAsync());
        Assert.Equal("Started", body.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Toggling_an_unknown_plugin_is_a_404()
    {
        await using var app = await StartGatewayAsync(new FakePluginHost(Module("energy", PluginState.Started)));

        var response = await app.GetTestClient().PostAsync(new Uri("/store/plugins/ghost/disable", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Presents_the_system_status_rolled_up_from_active_plugins()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                CapabilityModule("energy", PluginState.Started,
                    provides: ["energy.readmodel"],
                    consumes: ["MeterReadingReceived"],
                    emits: ["EnergySpikeDetected"]),
                CapabilityModule("dashboard", PluginState.Started,
                    provides: ["dashboard.board"],
                    consumes: ["EnergySpikeDetected", "OeeCalculated"],
                    emits: []),
                Module("off", PluginState.Disabled, Screen("hidden", 1))));

        await using var stream = await app.GetTestClient().GetStreamAsync(new Uri("/system", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var root = document.RootElement;
        Assert.Contains("FactoryOS", root.GetProperty("product").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("0.1", root.GetProperty("version").GetString(), StringComparison.Ordinal);
        Assert.Equal(3, root.GetProperty("modulesInstalled").GetInt32());
        Assert.Equal(2, root.GetProperty("modulesActive").GetInt32());
        Assert.Equal(0, root.GetProperty("pluginsNeedingAttention").GetInt32());

        // Capabilities are the sorted union across ACTIVE plugins only (the disabled one contributes nothing).
        var capabilities = root.GetProperty("capabilities").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Equal(["dashboard.board", "energy.readmodel"], capabilities);

        // Distinct event types across active plugins: MeterReadingReceived, EnergySpikeDetected, OeeCalculated.
        Assert.Equal(3, root.GetProperty("eventTypes").GetInt32());
    }

    [Fact]
    public async Task Navigation_is_filtered_by_the_callers_permissions()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                Module("dashboard", PluginState.Started, Screen("dash.ops", 1, section: "Experience")),
                Module("store", PluginState.Started, Screen("store.admin", 1, section: "Admin", permission: "admin.manage"))));

        var client = app.GetTestClient();

        // No permission header → unrestricted → both sections visible.
        using (var open = JsonDocument.Parse(await client.GetStringAsync(new Uri("/modules/ui/nav", UriKind.Relative))))
        {
            Assert.Equal(2, open.RootElement.GetProperty("sections").GetArrayLength());
        }

        // A session without admin.manage → the Admin section is filtered away entirely.
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/modules/ui/nav", UriKind.Relative));
        request.Headers.Add("X-FactoryOS-Permissions", "energy.view, quality.view");
        using var restricted = JsonDocument.Parse(await (await client.SendAsync(request)).Content.ReadAsStringAsync());

        var section = Assert.Single(restricted.RootElement.GetProperty("sections").EnumerateArray());
        Assert.Equal("Experience", section.GetProperty("section").GetString());
    }

    [Fact]
    public async Task A_resource_wildcard_permission_grants_every_action_on_that_module()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                Module("energy", PluginState.Started, Screen("energy.dash", 1, section: "Operations", permission: "energy.view")),
                Module("store", PluginState.Started, Screen("store.admin", 1, section: "Admin", permission: "store.manage"))));

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/modules/ui/nav", UriKind.Relative));
        request.Headers.Add("X-FactoryOS-Permissions", "energy.*"); // grants energy.view, not store.manage
        using var document = JsonDocument.Parse(await (await app.GetTestClient().SendAsync(request)).Content.ReadAsStringAsync());

        var section = Assert.Single(document.RootElement.GetProperty("sections").EnumerateArray());
        Assert.Equal("Operations", section.GetProperty("section").GetString());
        Assert.Equal("energy", Assert.Single(section.GetProperty("items").EnumerateArray()).GetProperty("module").GetString());
    }

    [Fact]
    public async Task Bootstraps_the_shell_with_tenant_nav_and_apis_in_one_call()
    {
        await using var app = await StartGatewayAsync(
            new FakePluginHost(
                ApiModule("activity", PluginState.Started, Route("GET", "/m/activity/feed", "tenant", "max")),
                Module("dashboard", PluginState.Started, Screen("dash.ops", 1, section: "Experience"))),
            new FakeModuleApi("dashboard"));

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/shell", UriKind.Relative));
        request.Headers.Add("X-FactoryOS-Tenant", "acme");
        var response = await app.GetTestClient().SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(document.RootElement.GetProperty("tenant").GetProperty("resolved").GetBoolean());
        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetProperty("tenant").GetString());

        var section = Assert.Single(document.RootElement.GetProperty("nav").GetProperty("sections").EnumerateArray());
        Assert.Equal("Experience", section.GetProperty("section").GetString());

        var api = Assert.Single(document.RootElement.GetProperty("apis").EnumerateArray());
        Assert.Equal("activity", api.GetProperty("key").GetString());

        // Neutral default branding: display name falls back to the tenant key.
        Assert.Equal("acme", document.RootElement.GetProperty("branding").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Bootstrap_carries_the_resolved_tenants_branding()
    {
        var branding = new TenantBrandingProvider([
            new TenantBranding("acme", "Acme Foods", "#e11d48", "https://cdn.example/acme.png"),
        ]);
        await using var app = await StartGatewayAsync(
            new FakePluginHost(Module("dashboard", PluginState.Started, Screen("dash.ops", 1))),
            branding,
            []);

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/shell", UriKind.Relative));
        request.Headers.Add("X-FactoryOS-Tenant", "acme");
        var response = await app.GetTestClient().SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var b = document.RootElement.GetProperty("branding");
        Assert.Equal("acme", b.GetProperty("tenant").GetString());
        Assert.Equal("Acme Foods", b.GetProperty("displayName").GetString());
        Assert.Equal("#e11d48", b.GetProperty("primaryColor").GetString());
        Assert.Equal("https://cdn.example/acme.png", b.GetProperty("logoUrl").GetString());
    }
}
