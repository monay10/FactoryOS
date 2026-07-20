using System.Net;
using System.Text.Json;
using FactoryOS.Agents.Insight;
using FactoryOS.Agents.Insight.Domain;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Insight agent's read model, queried over HTTP through the real gateway with zero core changes: the agent —
/// a plugin like any other — contributes <c>IModuleApi</c> endpoints the gateway mounts under <c>/m/insight/*</c>
/// purely from the manifest key. A screen reads the tenant's recent AI reasoning in one call, projected from the
/// <c>InsightGenerated</c> facts the agent both emits and folds back, never by touching the reasoning path.
/// </summary>
public sealed class InsightApiTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);

    private static InsightRecord Record(string trigger, string subject) =>
        new(Guid.NewGuid(), Guid.NewGuid(), trigger, subject, $"insight for {subject}", "model-x", At);

    private static async Task<WebApplication> StartAsync(Action<IInsightFeed> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("insight", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new InsightAgentPlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        seed(app.Services.GetRequiredService<IInsightFeed>());
        app.UseTenantResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Serves_the_feed_newest_first()
    {
        await using var app = await StartAsync(feed =>
        {
            feed.TryRecord("acme", Record("SafetyStandDownTriggered", "older"));
            feed.TryRecord("acme", Record("RuleTriggered", "newer"));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/insight/feed?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var items = document.RootElement.GetProperty("insights").EnumerateArray().ToArray();
        Assert.Equal("newer", items[0].GetProperty("subject").GetString());
        Assert.Equal("RuleTriggered", items[0].GetProperty("triggerType").GetString());
        Assert.Equal("older", items[1].GetProperty("subject").GetString());
    }

    [Fact]
    public async Task Caps_the_feed_by_max()
    {
        await using var app = await StartAsync(feed =>
        {
            feed.TryRecord("acme", Record("RuleTriggered", "a"));
            feed.TryRecord("acme", Record("RuleTriggered", "b"));
            feed.TryRecord("acme", Record("RuleTriggered", "c"));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/insight/feed?tenant=acme&max=1", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Single(document.RootElement.GetProperty("insights").EnumerateArray());
    }

    [Fact]
    public async Task Summarizes_by_trigger_type()
    {
        await using var app = await StartAsync(feed =>
        {
            feed.TryRecord("acme", Record("RuleTriggered", "a"));
            feed.TryRecord("acme", Record("RuleTriggered", "b"));
            feed.TryRecord("acme", Record("QualityAlertRaised", "c"));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/insight/summary?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(3, document.RootElement.GetProperty("total").GetInt32());
        var top = document.RootElement.GetProperty("byTrigger").EnumerateArray().First();
        Assert.Equal("RuleTriggered", top.GetProperty("triggerType").GetString());
        Assert.Equal(2, top.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/insight/feed", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Isolates_tenants()
    {
        await using var app = await StartAsync(feed => feed.TryRecord("acme", Record("RuleTriggered", "a")));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/insight/summary?tenant=globex", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal(0, document.RootElement.GetProperty("total").GetInt32());
    }
}
