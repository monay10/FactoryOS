using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Brain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The ask half of the Company Brain's HTTP surface, exercised over the real gateway and bus. <c>POST /m/brain/ask</c>
/// publishes a <see cref="BrainQuestionAsked"/> (asking decoupled from answering — the API touches no AI layer). A
/// stand-in "agent" answers on the bus with <see cref="BrainAnswered"/>, which the Brain plugin folds into its log;
/// <c>GET /m/brain/answers</c> then returns the grounded answer. Proves the full loop
/// HTTP ask → bus → answer → log → HTTP read with zero inter-module references.
/// </summary>
public sealed class BrainAskApiTests
{
    private sealed class CaptureSink
    {
        public ConcurrentBag<IIntegrationEvent> Events { get; } = [];
    }

    private sealed class CapturingHandler<TEvent> : IEventHandler<TEvent>
        where TEvent : IIntegrationEvent
    {
        private readonly CaptureSink _sink;

        public CapturingHandler(CaptureSink sink) => _sink = sink;

        public Task HandleAsync(TEvent integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _sink.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    /// <summary>Stands in for the Brain Query agent: turns a question into a canned grounded answer on the bus.</summary>
    private sealed class StubAnsweringHandler : IEventHandler<BrainQuestionAsked>
    {
        private readonly IEventBus _bus;

        public StubAnsweringHandler(IEventBus bus) => _bus = bus;

        public Task HandleAsync(BrainQuestionAsked integrationEvent, EventContext context, CancellationToken cancellationToken) =>
            _bus.PublishAsync(
                new BrainAnswered
                {
                    Tenant = integrationEvent.Tenant,
                    Question = integrationEvent.Question,
                    Answer = $"Grounded answer to: {integrationEvent.Question}",
                    Model = "fast",
                    AnsweredAt = integrationEvent.AskedAt,
                    SourceEventId = integrationEvent.EventId,
                },
                cancellationToken: cancellationToken);
    }

    private static async Task<WebApplication> StartAsync(bool withStubAgent)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new Gateway.FakePluginHost(Module("brain", PluginState.Started)));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        builder.Services.AddEventBus();
        new BrainPlugin().ConfigureServices(builder.Services);

        builder.Services.AddSingleton<CaptureSink>();
        builder.Services.AddScoped<IEventHandler<BrainQuestionAsked>, CapturingHandler<BrainQuestionAsked>>();
        if (withStubAgent)
        {
            builder.Services.AddScoped<IEventHandler<BrainQuestionAsked>, StubAnsweringHandler>();
        }

        var app = builder.Build();
        app.UseTenantResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Asking_publishes_a_question_on_the_bus_and_returns_the_question_id()
    {
        await using var app = await StartAsync(withStubAgent: false);

        var response = await app.GetTestClient().PostAsJsonAsync(
            new Uri("/m/brain/ask?tenant=acme", UriKind.Relative),
            new { question = "  Why did press-1 spike?  ", askedBy = "user:mehmet" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
        Assert.Equal("Why did press-1 spike?", document.RootElement.GetProperty("question").GetString());
        var questionId = document.RootElement.GetProperty("questionId").GetGuid();

        var sink = app.Services.GetRequiredService<CaptureSink>();
        var asked = Assert.Single(sink.Events.OfType<BrainQuestionAsked>());
        Assert.Equal("acme", asked.Tenant);
        Assert.Equal("Why did press-1 spike?", asked.Question); // trimmed
        Assert.Equal("user:mehmet", asked.AskedBy);
        Assert.Equal(questionId, asked.EventId);
    }

    [Fact]
    public async Task An_empty_question_is_rejected()
    {
        await using var app = await StartAsync(withStubAgent: false);

        var response = await app.GetTestClient().PostAsJsonAsync(
            new Uri("/m/brain/ask?tenant=acme", UriKind.Relative),
            new { question = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<CaptureSink>().Events.OfType<BrainQuestionAsked>());
    }

    [Fact]
    public async Task Asking_requires_a_tenant()
    {
        await using var app = await StartAsync(withStubAgent: false);

        var response = await app.GetTestClient().PostAsJsonAsync(
            new Uri("/m/brain/ask", UriKind.Relative),
            new { question = "anything" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_question_asked_over_http_comes_back_as_an_answer_over_http()
    {
        await using var app = await StartAsync(withStubAgent: true);
        var client = app.GetTestClient();

        var ask = await client.PostAsJsonAsync(
            new Uri("/m/brain/ask?tenant=acme", UriKind.Relative),
            new { question = "Why did press-1 spike?" });
        Assert.Equal(HttpStatusCode.Accepted, ask.StatusCode);

        await using var stream = await client.GetStreamAsync(new Uri("/m/brain/answers?tenant=acme", UriKind.Relative));
        using var document = await JsonDocument.ParseAsync(stream);

        var entry = Assert.Single(document.RootElement.GetProperty("answers").EnumerateArray());
        Assert.Equal("Why did press-1 spike?", entry.GetProperty("question").GetString());
        Assert.Equal("Grounded answer to: Why did press-1 spike?", entry.GetProperty("answer").GetString());
    }
}
