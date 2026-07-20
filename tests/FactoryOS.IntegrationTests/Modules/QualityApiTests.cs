using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Quality;
using FactoryOS.Plugins.Quality.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Quality defect-rate read model, queried over HTTP through the real gateway with zero core changes: the plugin
/// contributes <c>IModuleApi</c> endpoints the gateway mounts under <c>/m/quality/*</c> purely from the manifest key.
/// A line supervisor reads current per-line defect rates without referencing the inspection sources, and the breach
/// flag is decided by the same evaluator the alerting handler uses.
/// </summary>
public sealed class QualityApiTests
{
    // Options default: threshold 0.05, minimumInspectedUnits 20, windowSize 20.
    private static async Task<WebApplication> StartAsync(Action<IDefectRateWindowStore> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("quality", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new QualityPlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        seed(app.Services.GetRequiredService<IDefectRateWindowStore>());
        app.UseTenantResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Serves_per_line_defect_rates_flagging_breaches()
    {
        await using var app = await StartAsync(store =>
        {
            // line-1: 5/50 = 0.10 with 50 inspected (≥ min 20) → breach.
            store.Fold(new QualityLineKey("acme", "line-1", "P1"), inspectedUnits: 50, defectiveUnits: 5);
            // line-2: 1/50 = 0.02 → below threshold.
            store.Fold(new QualityLineKey("acme", "line-2", "P1"), inspectedUnits: 50, defectiveUnits: 1);
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/quality/lines?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var byLine = document.RootElement.GetProperty("lines").EnumerateArray()
            .ToDictionary(e => e.GetProperty("lineId").GetString()!, e => e);

        Assert.Equal(0.10m, byLine["line-1"].GetProperty("defectRate").GetDecimal());
        Assert.True(byLine["line-1"].GetProperty("breachesThreshold").GetBoolean());
        Assert.False(byLine["line-2"].GetProperty("breachesThreshold").GetBoolean());
    }

    [Fact]
    public async Task Does_not_flag_a_breach_without_enough_evidence()
    {
        await using var app = await StartAsync(store =>
            // 2/10 = 0.20 rate but only 10 inspected (< min 20) → not a breach yet.
            store.Fold(new QualityLineKey("acme", "line-1", "P1"), inspectedUnits: 10, defectiveUnits: 2));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/quality/lines?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var only = Assert.Single(document.RootElement.GetProperty("lines").EnumerateArray());
        Assert.False(only.GetProperty("breachesThreshold").GetBoolean());
    }

    [Fact]
    public async Task Filters_to_breaching_lines_only()
    {
        await using var app = await StartAsync(store =>
        {
            store.Fold(new QualityLineKey("acme", "line-1", "P1"), 50, 5);
            store.Fold(new QualityLineKey("acme", "line-2", "P1"), 50, 1);
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/quality/lines?tenant=acme&breaching=true", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var only = Assert.Single(document.RootElement.GetProperty("lines").EnumerateArray());
        Assert.Equal("line-1", only.GetProperty("lineId").GetString());
    }

    [Fact]
    public async Task Summarizes_lines_and_breaches()
    {
        await using var app = await StartAsync(store =>
        {
            store.Fold(new QualityLineKey("acme", "line-1", "P1"), 50, 5);
            store.Fold(new QualityLineKey("acme", "line-2", "P1"), 50, 1);
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/quality/summary?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(0.05m, document.RootElement.GetProperty("threshold").GetDecimal());
        Assert.Equal(2, document.RootElement.GetProperty("lines").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("breaching").GetInt32());
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/quality/summary", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Isolates_tenants()
    {
        await using var app = await StartAsync(store =>
            store.Fold(new QualityLineKey("acme", "line-1", "P1"), 50, 5));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/quality/summary?tenant=globex", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(0, document.RootElement.GetProperty("lines").GetInt32());
    }
}
