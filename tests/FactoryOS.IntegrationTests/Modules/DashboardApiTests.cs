using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Dashboard;
using FactoryOS.Plugins.Dashboard.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Dashboard operations board, queried over HTTP through the real gateway with zero core changes: the plugin
/// contributes <c>IModuleApi</c> endpoints the gateway mounts under <c>/m/dashboard/*</c> purely from the manifest
/// key. A wall screen reads the whole factory — latest OEE per machine and the live alert feed — in one call,
/// aggregated from the module events the board consumes, never by touching the modules themselves.
/// </summary>
public sealed class DashboardApiTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);

    private static async Task<WebApplication> StartAsync(Action<IOperationsBoard> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("dashboard", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new DashboardPlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        seed(app.Services.GetRequiredService<IOperationsBoard>());
        app.UseTenantResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Serves_machines_ordered_and_alerts_newest_first()
    {
        await using var app = await StartAsync(board =>
        {
            board.RecordOee("acme", new OeeTile("m-2", 0.72m, false, At));
            board.RecordOee("acme", new OeeTile("m-1", 0.88m, true, At));
            board.PushAlert("acme", new AlertTile("LowStockDetected", AlertLevels.Warning, "SKU-9 low", At));
            board.PushAlert("acme", new AlertTile("SafetyStandDownTriggered", AlertLevels.Critical, "Line 3 halted", At.AddMinutes(5)));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/dashboard/board?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var machines = document.RootElement.GetProperty("machines").EnumerateArray().ToArray();
        Assert.Equal("m-1", machines[0].GetProperty("machineId").GetString());
        Assert.Equal("m-2", machines[1].GetProperty("machineId").GetString());

        var alerts = document.RootElement.GetProperty("alerts").EnumerateArray().ToArray();
        Assert.Equal("SafetyStandDownTriggered", alerts[0].GetProperty("kind").GetString());
        Assert.Equal("LowStockDetected", alerts[1].GetProperty("kind").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("criticalAlertCount").GetInt32());
    }

    [Fact]
    public async Task Filters_the_alert_feed_by_level_but_keeps_the_critical_headline()
    {
        await using var app = await StartAsync(board =>
        {
            board.PushAlert("acme", new AlertTile("LowStockDetected", AlertLevels.Warning, "SKU-9 low", At));
            board.PushAlert("acme", new AlertTile("SafetyStandDownTriggered", AlertLevels.Critical, "Line 3 halted", At));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/dashboard/board?tenant=acme&level=Critical", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var only = Assert.Single(document.RootElement.GetProperty("alerts").EnumerateArray());
        Assert.Equal(AlertLevels.Critical, only.GetProperty("level").GetString());
        // The filter narrows the feed, but the headline still counts the whole board.
        Assert.Equal(1, document.RootElement.GetProperty("criticalAlertCount").GetInt32());
    }

    [Fact]
    public async Task Filters_the_alert_feed_by_kind_composably_with_level()
    {
        await using var app = await StartAsync(board =>
        {
            board.PushAlert("acme", new AlertTile("EnergySpikeDetected", AlertLevels.Warning, "press-1 spike", At));
            board.PushAlert("acme", new AlertTile("LowStockDetected", AlertLevels.Warning, "SKU-9 low", At.AddMinutes(1)));
            board.PushAlert("acme", new AlertTile("SafetyStandDownTriggered", AlertLevels.Critical, "Line 3 halted", At.AddMinutes(2)));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/dashboard/board?tenant=acme&kind=EnergySpikeDetected", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var only = Assert.Single(document.RootElement.GetProperty("alerts").EnumerateArray());
        Assert.Equal("EnergySpikeDetected", only.GetProperty("kind").GetString());
        // The kind filter narrows the feed, but the headline still counts the whole board.
        Assert.Equal(1, document.RootElement.GetProperty("criticalAlertCount").GetInt32());
    }

    [Fact]
    public async Task Summarizes_machines_below_target_and_alerts()
    {
        await using var app = await StartAsync(board =>
        {
            board.RecordOee("acme", new OeeTile("m-1", 0.88m, true, At));
            board.RecordOee("acme", new OeeTile("m-2", 0.72m, false, At));
            board.PushAlert("acme", new AlertTile("LowStockDetected", AlertLevels.Warning, "SKU-9 low", At));
            board.PushAlert("acme", new AlertTile("SafetyStandDownTriggered", AlertLevels.Critical, "Line 3 halted", At));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/dashboard/summary?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(2, document.RootElement.GetProperty("machines").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("machinesBelowTarget").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("recentAlerts").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("criticalAlerts").GetInt32());
    }

    [Fact]
    public async Task Summary_breaks_the_feed_down_by_kind_count_descending()
    {
        await using var app = await StartAsync(board =>
        {
            board.PushAlert("acme", new AlertTile("EnergySpikeDetected", AlertLevels.Warning, "press-1 spike", At));
            board.PushAlert("acme", new AlertTile("EnergySpikeDetected", AlertLevels.Warning, "press-2 spike", At.AddMinutes(1)));
            board.PushAlert("acme", new AlertTile("SafetyStandDownTriggered", AlertLevels.Critical, "Line 3 halted", At.AddMinutes(2)));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/dashboard/summary?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var byKind = document.RootElement.GetProperty("alertsByKind").EnumerateArray().ToArray();
        Assert.Equal("EnergySpikeDetected", byKind[0].GetProperty("kind").GetString());
        Assert.Equal(2, byKind[0].GetProperty("count").GetInt32());
        Assert.Equal("SafetyStandDownTriggered", byKind[1].GetProperty("kind").GetString());
        Assert.Equal(1, byKind[1].GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/dashboard/summary", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Isolates_tenants()
    {
        await using var app = await StartAsync(board =>
            board.RecordOee("acme", new OeeTile("m-1", 0.88m, true, At)));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/dashboard/summary?tenant=globex", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(0, document.RootElement.GetProperty("machines").GetInt32());
    }
}
