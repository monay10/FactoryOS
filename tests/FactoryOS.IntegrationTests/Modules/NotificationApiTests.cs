using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Notification;
using FactoryOS.Plugins.Notification.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The notification outbox, queried over HTTP through the real gateway with zero core changes: the Notification
/// plugin contributes an <c>IModuleApi</c> the gateway mounts under <c>/m/notification/*</c> from the manifest key.
/// An operator reads the dispatched-notification history without referencing the transport connectors.
/// </summary>
public sealed class NotificationApiTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);

    private static async Task<WebApplication> StartAsync(Action<INotificationOutbox> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("notification", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new NotificationPlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        app.UseTenantResolution();
        seed(app.Services.GetRequiredService<INotificationOutbox>());
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    private static NotificationRecord Record(string subject, DateTimeOffset at) =>
        new("ops", "sms", "Normal", subject, "Notify", at);

    [Fact]
    public async Task Serves_the_outbox_newest_first_and_honours_max()
    {
        await using var app = await StartAsync(outbox =>
        {
            outbox.TryRecord("acme", Guid.NewGuid(), Record("older", At));
            outbox.TryRecord("acme", Guid.NewGuid(), Record("newer", At.AddMinutes(1)));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/notification/outbox?tenant=acme&max=1", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        var record = Assert.Single(document.RootElement.GetProperty("notifications").EnumerateArray());
        Assert.Equal("newer", record.GetProperty("subject").GetString());
        Assert.Equal("sms", record.GetProperty("transport").GetString());
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/notification/outbox", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Isolates_tenants()
    {
        await using var app = await StartAsync(outbox =>
            outbox.TryRecord("acme", Guid.NewGuid(), Record("s", At)));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/notification/outbox?tenant=globex", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Empty(document.RootElement.GetProperty("notifications").EnumerateArray());
    }
}
