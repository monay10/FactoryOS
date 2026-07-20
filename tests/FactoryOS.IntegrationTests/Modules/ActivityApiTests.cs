using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The factory timeline, queried over HTTP through the real gateway with zero core changes: the Activity plugin
/// contributes <c>IModuleApi</c> endpoints that the gateway mounts under <c>/m/activity/*</c> from the manifest key.
/// A UI or an operator reads the newest-first activity stream without referencing any producing module.
/// </summary>
public sealed class ActivityApiTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);

    private static async Task<WebApplication> StartAsync(Action<IActivityFeed> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("activity", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new ActivityPlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        app.UseTenantResolution();
        seed(app.Services.GetRequiredService<IActivityFeed>());
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Serves_the_timeline_newest_first_and_honours_max()
    {
        await using var app = await StartAsync(feed =>
        {
            feed.Record(new ActivityEntry("acme", "Rule", "older", At, Guid.NewGuid()));
            feed.Record(new ActivityEntry("acme", "Delivery", "newer", At.AddMinutes(1), Guid.NewGuid()));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/activity/feed?tenant=acme&max=1", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        var entry = Assert.Single(document.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal("newer", entry.GetProperty("headline").GetString());
        Assert.Equal("Delivery", entry.GetProperty("category").GetString());
    }

    [Fact]
    public async Task Narrows_the_timeline_to_a_category()
    {
        await using var app = await StartAsync(feed =>
        {
            feed.Record(new ActivityEntry("acme", "Rule", "a-rule", At, Guid.NewGuid()));
            feed.Record(new ActivityEntry("acme", "Production", "an-order", At.AddMinutes(1), Guid.NewGuid()));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/activity/feed?tenant=acme&category=production", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var entry = Assert.Single(document.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal("an-order", entry.GetProperty("headline").GetString());
        Assert.Equal("Production", entry.GetProperty("category").GetString());
    }

    [Fact]
    public async Task Summarizes_the_timeline_by_category()
    {
        await using var app = await StartAsync(feed =>
        {
            feed.Record(new ActivityEntry("acme", "Production", "an-order", At, Guid.NewGuid()));
            feed.Record(new ActivityEntry("acme", "Production", "another-order", At.AddMinutes(1), Guid.NewGuid()));
            feed.Record(new ActivityEntry("acme", "Insight", "an-insight", At.AddMinutes(2), Guid.NewGuid()));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/activity/summary?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("total").GetInt32());
        var top = document.RootElement.GetProperty("byCategory").EnumerateArray().First();
        Assert.Equal("Production", top.GetProperty("category").GetString());
        Assert.Equal(2, top.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/activity/feed", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Isolates_tenants()
    {
        await using var app = await StartAsync(feed =>
            feed.Record(new ActivityEntry("acme", "Rule", "h", At, Guid.NewGuid())));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/activity/feed?tenant=globex", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Empty(document.RootElement.GetProperty("entries").EnumerateArray());
    }
}
