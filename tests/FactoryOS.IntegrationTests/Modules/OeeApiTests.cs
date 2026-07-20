using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Oee;
using FactoryOS.Plugins.Oee.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The OEE read model, queried over HTTP through the real gateway with zero core changes: the plugin contributes
/// <c>IModuleApi</c> endpoints the gateway mounts under <c>/m/oee/*</c> purely from the manifest key. A wall dashboard
/// reads per-machine effectiveness and a factory-wide rollup — the first operations module to serve its read model —
/// with the tenant taken from the ambient context and each row judged against the configured target.
/// </summary>
public sealed class OeeApiTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 20, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 7, 20, 14, 0, 0, TimeSpan.Zero);

    private static async Task<WebApplication> StartAsync(Action<IOeeStore> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("oee", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new OeePlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        seed(app.Services.GetRequiredService<IOeeStore>());
        app.UseTenantResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    private static OeeSnapshot Snapshot(string tenant, string machine, decimal oee) =>
        new(tenant, machine, Start, End, new OeeScore(0.95m, 0.95m, oee / (0.95m * 0.95m), oee));

    [Fact]
    public async Task Serves_snapshots_for_a_tenant_ordered_and_judged_against_target()
    {
        await using var app = await StartAsync(store =>
        {
            store.TryAdd(Snapshot("acme", "line-2", 0.70m));
            store.TryAdd(Snapshot("acme", "line-1", 0.90m));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/oee/snapshots?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        var rows = document.RootElement.GetProperty("snapshots").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);

        // Ordered by machine id.
        Assert.Equal("line-1", rows[0].GetProperty("machineId").GetString());
        Assert.True(rows[0].GetProperty("meetsTarget").GetBoolean());
        Assert.Equal("line-2", rows[1].GetProperty("machineId").GetString());
        Assert.False(rows[1].GetProperty("meetsTarget").GetBoolean());
    }

    [Fact]
    public async Task Summarizes_effectiveness_against_the_target()
    {
        await using var app = await StartAsync(store =>
        {
            store.TryAdd(Snapshot("acme", "line-1", 0.90m));
            store.TryAdd(Snapshot("acme", "line-2", 0.70m));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/oee/summary?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(0.85m, document.RootElement.GetProperty("target").GetDecimal());
        Assert.Equal(2, document.RootElement.GetProperty("snapshots").GetInt32());
        Assert.Equal(0.80m, document.RootElement.GetProperty("averageOee").GetDecimal());
        Assert.Equal(1, document.RootElement.GetProperty("belowTarget").GetInt32());
    }

    [Fact]
    public async Task Resolves_the_tenant_from_the_header()
    {
        await using var app = await StartAsync(store => store.TryAdd(Snapshot("acme", "line-1", 0.90m)));

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/m/oee/snapshots", UriKind.Relative));
        request.Headers.Add("X-FactoryOS-Tenant", "acme");
        var response = await app.GetTestClient().SendAsync(request);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        Assert.Single(document.RootElement.GetProperty("snapshots").EnumerateArray());
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/oee/summary", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Isolates_tenants()
    {
        await using var app = await StartAsync(store => store.TryAdd(Snapshot("acme", "line-1", 0.90m)));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/oee/summary?tenant=globex", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(0, document.RootElement.GetProperty("snapshots").GetInt32());
        Assert.Equal(0m, document.RootElement.GetProperty("averageOee").GetDecimal());
    }
}
