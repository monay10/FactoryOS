using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Energy;
using FactoryOS.Plugins.Energy.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Energy read model, queried over HTTP through the real gateway with zero core changes: the plugin contributes
/// <c>IModuleApi</c> endpoints the gateway mounts under <c>/m/energy/*</c> purely from the manifest key. The Energy
/// dashboard reads the latest reading per meter and the recent-spike feed — the tenant taken from the ambient context —
/// without touching the Edge Gateway or a meter.
/// </summary>
public sealed class EnergyApiTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);

    private static async Task<WebApplication> StartAsync(Action<IEnergyReadModel> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("energy", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new EnergyPlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        seed(app.Services.GetRequiredService<IEnergyReadModel>());
        app.UseTenantResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Serves_the_latest_reading_per_meter_with_its_delta()
    {
        await using var app = await StartAsync(rm =>
        {
            rm.RecordReading(new EnergyMeterReading("acme", "main-incomer", "ActivePower", 120m, 100m, "kW", At));
            rm.RecordReading(new EnergyMeterReading("acme", "chiller-1", "ActivePower", 45m, 50m, "kW", At));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/energy/meters?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var meters = document.RootElement.GetProperty("meters").EnumerateArray().ToArray();
        // Ordered by meter id (Ordinal): chiller-1 before main-incomer.
        Assert.Equal("chiller-1", meters[0].GetProperty("meterId").GetString());
        Assert.Equal(-10, meters[0].GetProperty("deltaPercent").GetDecimal()); // 45 vs 50 baseline
        Assert.Equal("main-incomer", meters[1].GetProperty("meterId").GetString());
        Assert.Equal(20, meters[1].GetProperty("deltaPercent").GetDecimal()); // 120 vs 100 baseline
    }

    [Fact]
    public async Task Serves_recent_spikes_newest_first()
    {
        await using var app = await StartAsync(rm =>
        {
            rm.RecordSpike(new EnergySpikeEntry("acme", "main-incomer", "ActivePower", 150m, 100m, 50m, "kW", At));
            rm.RecordSpike(new EnergySpikeEntry("acme", "main-incomer", "ActivePower", 200m, 100m, 100m, "kW", At.AddMinutes(5)));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/energy/spikes?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var spikes = document.RootElement.GetProperty("spikes").EnumerateArray().ToArray();
        Assert.Equal(200, spikes[0].GetProperty("value").GetDecimal()); // newest first
        Assert.Equal(150, spikes[1].GetProperty("value").GetDecimal());
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/energy/meters", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
