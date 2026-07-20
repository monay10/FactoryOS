using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Maintenance;
using FactoryOS.Plugins.Maintenance.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Maintenance work-order backlog, queried over HTTP through the real gateway with zero core changes: the plugin
/// contributes <c>IModuleApi</c> endpoints the gateway mounts under <c>/m/maintenance/*</c> purely from the manifest
/// key. A technician's screen reads the to-do list without referencing the modules (Energy, Rule Engine) that raise it.
/// </summary>
public sealed class MaintenanceApiTests
{
    private static readonly DateTimeOffset Due = new(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);

    private static async Task<WebApplication> StartAsync(Action<IWorkOrderStore> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("maintenance", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new MaintenancePlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        seed(app.Services.GetRequiredService<IWorkOrderStore>());
        app.UseTenantResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    private static WorkOrder WorkOrder(string tenant, string number, string status, DateTimeOffset? dueAt) =>
        new()
        {
            Tenant = tenant,
            Number = number,
            Title = $"Inspect {number}",
            Status = status,
            AssetCode = "PUMP-1",
            DueAt = dueAt,
        };

    [Fact]
    public async Task Serves_the_backlog_soonest_due_first()
    {
        await using var app = await StartAsync(store =>
        {
            store.TryAdd(WorkOrder("acme", "WO-2", "Open", Due.AddHours(2)));
            store.TryAdd(WorkOrder("acme", "WO-1", "Open", Due));
            store.TryAdd(WorkOrder("acme", "WO-3", "Open", dueAt: null));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/maintenance/workorders?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        var numbers = document.RootElement.GetProperty("workOrders").EnumerateArray()
            .Select(e => e.GetProperty("number").GetString())
            .ToList();
        Assert.Equal(3, numbers.Count);
        Assert.Equal("WO-1", numbers[0]);
        Assert.Equal("WO-2", numbers[1]);
        Assert.Equal("WO-3", numbers[2]);
    }

    [Fact]
    public async Task Filters_the_backlog_by_status_case_insensitively()
    {
        await using var app = await StartAsync(store =>
        {
            store.TryAdd(WorkOrder("acme", "WO-1", "Open", Due));
            store.TryAdd(WorkOrder("acme", "WO-2", "Closed", Due));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/maintenance/workorders?tenant=acme&status=open", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var only = Assert.Single(document.RootElement.GetProperty("workOrders").EnumerateArray());
        Assert.Equal("WO-1", only.GetProperty("number").GetString());
    }

    [Fact]
    public async Task Summarizes_the_backlog_by_status()
    {
        await using var app = await StartAsync(store =>
        {
            store.TryAdd(WorkOrder("acme", "WO-1", "Open", Due));
            store.TryAdd(WorkOrder("acme", "WO-2", "Open", Due));
            store.TryAdd(WorkOrder("acme", "WO-3", "Closed", Due));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/maintenance/summary?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(3, document.RootElement.GetProperty("total").GetInt32());
        var byStatus = document.RootElement.GetProperty("byStatus").EnumerateArray()
            .ToDictionary(e => e.GetProperty("status").GetString()!, e => e.GetProperty("count").GetInt32());
        Assert.Equal(2, byStatus["Open"]);
        Assert.Equal(1, byStatus["Closed"]);
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/maintenance/workorders", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Isolates_tenants()
    {
        await using var app = await StartAsync(store =>
            store.TryAdd(WorkOrder("acme", "WO-1", "Open", Due)));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/maintenance/summary?tenant=globex", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(0, document.RootElement.GetProperty("total").GetInt32());
    }
}
