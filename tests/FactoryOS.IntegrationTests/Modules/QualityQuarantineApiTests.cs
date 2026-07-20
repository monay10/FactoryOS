using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Quality;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The write side of Quality, authorized at the API boundary — the same <c>RequirePermission</c> primitive as
/// Maintenance's close, proving it generalizes. Quarantining a line requires <c>quality.quarantine</c>: a request
/// without it is rejected with <c>403</c>. On a real transition the module announces <see cref="QualityLineQuarantined"/>
/// on the bus (no other module referenced), and re-quarantining changes and announces nothing.
/// </summary>
public sealed class QualityQuarantineApiTests
{
    private sealed class CaptureSink
    {
        public ConcurrentBag<QualityLineQuarantined> Quarantined { get; } = [];
    }

    private sealed class CapturingHandler : IEventHandler<QualityLineQuarantined>
    {
        private readonly CaptureSink _sink;

        public CapturingHandler(CaptureSink sink) => _sink = sink;

        public Task HandleAsync(QualityLineQuarantined integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _sink.Quarantined.Add(integrationEvent);
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
        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("quality", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        builder.Services.AddPermissionResolution();
        builder.Services.AddEventBus();
        new QualityPlugin().ConfigureServices(builder.Services);
        builder.Services.AddScoped<IEventHandler<QualityLineQuarantined>, CapturingHandler>();

        var app = builder.Build();
        app.UseTenantResolution();
        app.UsePermissionResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return (app, sink);
    }

    private static HttpRequestMessage Quarantine(string lineId, string? permissions)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/m/quality/lines/{lineId}/quarantine?tenant=acme", UriKind.Relative));
        if (permissions is not null)
        {
            request.Headers.Add("X-FactoryOS-Permissions", permissions);
        }

        return request;
    }

    [Fact]
    public async Task Quarantining_with_the_permission_announces_it_and_marks_the_line()
    {
        var (app, sink) = await StartAsync();
        await using var _ = app;

        var response = await app.GetTestClient().SendAsync(Quarantine("line-1", "quality.quarantine"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("quarantined").GetBoolean());
        Assert.True(document.RootElement.GetProperty("newlyQuarantined").GetBoolean());

        var announced = Assert.Single(sink.Quarantined);
        Assert.Equal("line-1", announced.LineId);
        Assert.Equal("acme", announced.Tenant);
    }

    [Fact]
    public async Task Quarantining_without_the_permission_is_forbidden_and_announces_nothing()
    {
        var (app, sink) = await StartAsync();
        await using var _ = app;

        // Holds only the read permission — enough to see the dashboard, not to hold a line.
        var response = await app.GetTestClient().SendAsync(Quarantine("line-1", "quality.view"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(sink.Quarantined);
    }

    [Fact]
    public async Task A_wildcard_grants_the_write()
    {
        var (app, sink) = await StartAsync();
        await using var _ = app;

        var response = await app.GetTestClient().SendAsync(Quarantine("line-1", "quality.*"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(sink.Quarantined);
    }

    [Fact]
    public async Task Quarantining_twice_is_idempotent_and_announces_once()
    {
        var (app, sink) = await StartAsync();
        await using var _ = app;

        var first = await app.GetTestClient().SendAsync(Quarantine("line-1", "quality.quarantine"));
        var second = await app.GetTestClient().SendAsync(Quarantine("line-1", "quality.quarantine"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var secondDoc = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.True(secondDoc.RootElement.GetProperty("quarantined").GetBoolean());
        Assert.False(secondDoc.RootElement.GetProperty("newlyQuarantined").GetBoolean()); // no transition the second time
        Assert.Single(sink.Quarantined);
    }
}
