using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Events;
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
/// The write side of Maintenance, authorized at the API boundary. Closing a work order requires the
/// <c>maintenance.close</c> permission — not merely a visible screen — so a hand-crafted request without it is
/// rejected with <c>403</c>. On a real transition the module announces <c>WorkOrderClosed</c> on the bus (no other
/// module is referenced), and closing is idempotent: a repeat close changes nothing and re-announces nothing.
/// </summary>
public sealed class MaintenanceCloseApiTests
{
    private sealed class CaptureSink
    {
        public ConcurrentBag<WorkOrderClosed> Closed { get; } = [];
    }

    private sealed class CapturingHandler : IEventHandler<WorkOrderClosed>
    {
        private readonly CaptureSink _sink;

        public CapturingHandler(CaptureSink sink) => _sink = sink;

        public Task HandleAsync(WorkOrderClosed integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _sink.Closed.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private static async Task<(WebApplication App, CaptureSink Sink)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddLogging();

        var sink = new CaptureSink();
        builder.Services.AddSingleton(sink);
        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("maintenance", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        builder.Services.AddPermissionResolution();
        builder.Services.AddEventBus();
        new MaintenancePlugin().ConfigureServices(builder.Services);
        builder.Services.AddScoped<IEventHandler<WorkOrderClosed>, CapturingHandler>();

        var app = builder.Build();
        app.Services.GetRequiredService<IWorkOrderStore>().TryAdd(new WorkOrder
        {
            Tenant = "acme",
            Number = "WO-1",
            Title = "Inspect PUMP-1",
            Status = "Open",
            AssetCode = "PUMP-1",
        });

        app.UseTenantResolution();
        app.UsePermissionResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return (app, sink);
    }

    private static HttpRequestMessage Close(string number, string? permissions)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/m/maintenance/workorders/{number}/close?tenant=acme", UriKind.Relative));
        if (permissions is not null)
        {
            request.Headers.Add("X-FactoryOS-Permissions", permissions);
        }

        return request;
    }

    [Fact]
    public async Task Closing_with_the_permission_transitions_the_order_and_announces_it()
    {
        var (app, sink) = await StartAsync();
        await using var _ = app;

        var response = await app.GetTestClient().SendAsync(Close("WO-1", "maintenance.close"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Closed", document.RootElement.GetProperty("status").GetString());

        var announced = Assert.Single(sink.Closed);
        Assert.Equal("WO-1", announced.WorkOrder.Number);
        Assert.Equal("Closed", announced.WorkOrder.Status);
    }

    [Fact]
    public async Task Closing_without_the_permission_is_forbidden_and_announces_nothing()
    {
        var (app, sink) = await StartAsync();
        await using var _ = app;

        // Holds only the read permission — enough to see the screen, not to perform the write.
        var response = await app.GetTestClient().SendAsync(Close("WO-1", "maintenance.view"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(sink.Closed);
    }

    [Fact]
    public async Task Closing_an_unknown_order_is_not_found()
    {
        var (app, _) = await StartAsync();
        await using var __ = app;

        var response = await app.GetTestClient().SendAsync(Close("WO-404", "maintenance.*"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Closing_twice_is_idempotent_and_announces_once()
    {
        var (app, sink) = await StartAsync();
        await using var _ = app;

        var first = await app.GetTestClient().SendAsync(Close("WO-1", "maintenance.close"));
        var second = await app.GetTestClient().SendAsync(Close("WO-1", "maintenance.close"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(sink.Closed);
    }
}
