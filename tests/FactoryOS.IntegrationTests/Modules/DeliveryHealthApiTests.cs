using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.DeliveryHealth;
using FactoryOS.Plugins.DeliveryHealth.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Delivery Health read model, queried over HTTP through the real gateway with zero core changes: the plugin
/// contributes <c>IModuleApi</c> endpoints that the gateway mounts under <c>/m/deliveryhealth/*</c> purely from the
/// manifest key. An operator reads transport health and recent failure detail without referencing the connectors or
/// the Notification module.
/// </summary>
public sealed class DeliveryHealthApiTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);

    private static async Task<WebApplication> StartAsync(Action<IDeliveryHealthStore> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("deliveryhealth", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new DeliveryHealthPlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        seed(app.Services.GetRequiredService<IDeliveryHealthStore>());
        app.UseTenantResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Serves_per_transport_health_for_a_tenant()
    {
        await using var app = await StartAsync(store =>
        {
            store.Record("acme", Guid.NewGuid(), "webhook", "ops", "Pump alert", delivered: false, "503", At);
            store.Record("acme", Guid.NewGuid(), "webhook", "ops", "Pump alert", delivered: true, null, At);
            store.Record("acme", Guid.NewGuid(), "log", "audit", "s", delivered: true, null, At);
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/deliveryhealth/health?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        var byTransport = document.RootElement.GetProperty("transports").EnumerateArray()
            .ToDictionary(e => e.GetProperty("transport").GetString()!, e => e);

        Assert.Equal(2, byTransport["webhook"].GetProperty("attempts").GetInt32());
        Assert.Equal(1, byTransport["webhook"].GetProperty("failed").GetInt32());
        Assert.Equal(1, byTransport["log"].GetProperty("delivered").GetInt32());
    }

    [Fact]
    public async Task Serves_recent_failures_newest_first()
    {
        await using var app = await StartAsync(store =>
        {
            store.Record("acme", Guid.NewGuid(), "webhook", "ops", "first", delivered: false, "500", At);
            store.Record("acme", Guid.NewGuid(), "webhook", "ops", "second", delivered: false, "502", At);
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/deliveryhealth/failures?tenant=acme&max=1", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var failure = Assert.Single(document.RootElement.GetProperty("failures").EnumerateArray());
        Assert.Equal("second", failure.GetProperty("subject").GetString());
        Assert.Equal("502", failure.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Resolves_the_tenant_from_the_header()
    {
        await using var app = await StartAsync(store =>
            store.Record("acme", Guid.NewGuid(), "log", "audit", "s", delivered: true, null, At));

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/m/deliveryhealth/health", UriKind.Relative));
        request.Headers.Add("X-FactoryOS-Tenant", "acme");
        var response = await app.GetTestClient().SendAsync(request);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        Assert.Single(document.RootElement.GetProperty("transports").EnumerateArray());
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/deliveryhealth/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Isolates_tenants()
    {
        await using var app = await StartAsync(store =>
            store.Record("acme", Guid.NewGuid(), "log", "audit", "s", delivered: true, null, At));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/deliveryhealth/health?tenant=globex", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Empty(document.RootElement.GetProperty("transports").EnumerateArray());
    }
}
