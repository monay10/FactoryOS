using System.Net;
using System.Text.Json;
using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Brain;
using FactoryOS.Plugins.Brain.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Company Brain's grounded Q&amp;A history, queried over HTTP through the real gateway with zero core changes:
/// the Brain plugin contributes <c>IModuleApi</c> endpoints that the gateway mounts under <c>/m/brain/*</c> from the
/// manifest key. A UI reads the newest-first answer stream without referencing the AI layer that produced it.
/// </summary>
public sealed class BrainApiTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyList<BrainCitation> NoCitations = [];

    private static BrainAnswerEntry Entry(string tenant, string question, DateTimeOffset at) =>
        new(tenant, question, $"answer to {question}", "fast", NoCitations, at, Guid.NewGuid());

    private static async Task<WebApplication> StartAsync(Action<IBrainAnswerLog> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("brain", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        new BrainPlugin().ConfigureServices(builder.Services);

        var app = builder.Build();
        app.UseTenantResolution();
        seed(app.Services.GetRequiredService<IBrainAnswerLog>());
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Serves_the_answers_newest_first_and_honours_max()
    {
        await using var app = await StartAsync(log =>
        {
            log.Record(Entry("acme", "older", At));
            log.Record(Entry("acme", "newer", At.AddMinutes(1)));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/brain/answers?tenant=acme&max=1", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        var entry = Assert.Single(document.RootElement.GetProperty("answers").EnumerateArray());
        Assert.Equal("newer", entry.GetProperty("question").GetString());
        Assert.Equal("fast", entry.GetProperty("model").GetString());
    }

    [Fact]
    public async Task Summarizes_the_answer_log_by_model()
    {
        await using var app = await StartAsync(log =>
        {
            log.Record(new BrainAnswerEntry("acme", "q1", "a1", "fast", NoCitations, At, Guid.NewGuid()));
            log.Record(new BrainAnswerEntry("acme", "q2", "a2", "fast", NoCitations, At.AddMinutes(1), Guid.NewGuid()));
            log.Record(new BrainAnswerEntry("acme", "q3", "a3", "reasoning", NoCitations, At.AddMinutes(2), Guid.NewGuid()));
        });

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/brain/summary?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("total").GetInt32());
        var top = document.RootElement.GetProperty("byModel").EnumerateArray().First();
        Assert.Equal("fast", top.GetProperty("model").GetString());
        Assert.Equal(2, top.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Requires_a_tenant()
    {
        await using var app = await StartAsync(static _ => { });

        var response = await app.GetTestClient().GetAsync(new Uri("/m/brain/answers", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Isolates_tenants()
    {
        await using var app = await StartAsync(log => log.Record(Entry("acme", "q", At)));

        await using var stream = await app.GetTestClient()
            .GetStreamAsync(new Uri("/m/brain/answers?tenant=globex", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.Empty(document.RootElement.GetProperty("answers").EnumerateArray());
    }
}
