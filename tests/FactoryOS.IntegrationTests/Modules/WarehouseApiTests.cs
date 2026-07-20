using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Warehouse;
using FactoryOS.Plugins.Warehouse.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Warehouse stock read model, queried over HTTP through the real gateway with zero core changes: the plugin
/// contributes <c>IModuleApi</c> endpoints the gateway mounts under <c>/m/warehouse/*</c> purely from the manifest
/// key. A stock controller reads on-hand levels and what is at/below reorder without referencing the ERP connectors.
/// </summary>
public sealed class WarehouseApiTests
{
    private static async Task<WebApplication> StartAsync(Action<IStockLedger> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("warehouse", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new WarehousePlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        seed(app.Services.GetRequiredService<IStockLedger>());
        app.UseTenantResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    private static void Seed(IStockLedger ledger, string tenant, string sku, decimal onHand, decimal? reorderPoint)
    {
        var key = new WarehouseStockKey(tenant, "WH-1", sku);
        ledger.Apply(key, onHand);
        if (reorderPoint is { } point)
        {
            ledger.SetReorderPoint(key, point);
        }
    }

    [Fact]
    public async Task Serves_stock_levels_flagging_items_below_reorder()
    {
        await using var app = await StartAsync(ledger =>
        {
            Seed(ledger, "acme", "SKU-A", onHand: 3m, reorderPoint: 5m);   // below
            Seed(ledger, "acme", "SKU-B", onHand: 20m, reorderPoint: 5m);  // healthy
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/warehouse/stock?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var bySku = document.RootElement.GetProperty("items").EnumerateArray()
            .ToDictionary(e => e.GetProperty("sku").GetString()!, e => e);

        Assert.Equal(3m, bySku["SKU-A"].GetProperty("onHand").GetDecimal());
        Assert.True(bySku["SKU-A"].GetProperty("belowReorder").GetBoolean());
        Assert.False(bySku["SKU-B"].GetProperty("belowReorder").GetBoolean());
    }

    [Fact]
    public async Task Filters_to_items_below_reorder_only()
    {
        await using var app = await StartAsync(ledger =>
        {
            Seed(ledger, "acme", "SKU-A", onHand: 3m, reorderPoint: 5m);
            Seed(ledger, "acme", "SKU-B", onHand: 20m, reorderPoint: 5m);
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/warehouse/stock?tenant=acme&belowReorder=true", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var only = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("SKU-A", only.GetProperty("sku").GetString());
    }

    [Fact]
    public async Task Summarizes_stock_and_low_items()
    {
        await using var app = await StartAsync(ledger =>
        {
            Seed(ledger, "acme", "SKU-A", onHand: 3m, reorderPoint: 5m);
            Seed(ledger, "acme", "SKU-B", onHand: 20m, reorderPoint: 5m);
            Seed(ledger, "acme", "SKU-C", onHand: 0m, reorderPoint: 2m);
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/warehouse/summary?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(3, document.RootElement.GetProperty("trackedItems").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("belowReorder").GetInt32());
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/warehouse/stock", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Isolates_tenants()
    {
        await using var app = await StartAsync(ledger => Seed(ledger, "acme", "SKU-A", 3m, 5m));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/warehouse/summary?tenant=globex", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(0, document.RootElement.GetProperty("trackedItems").GetInt32());
    }
}
