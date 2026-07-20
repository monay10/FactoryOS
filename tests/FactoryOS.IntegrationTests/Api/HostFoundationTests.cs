using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryOS.Api.Hosting;
using FactoryOS.Shared.Constants;
using FactoryOS.Shared.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryOS.IntegrationTests.Api;

/// <summary>
/// The ASP.NET Core host foundation end-to-end: a minimal host that wires only the foundation (no plugins, identity or
/// database) must expose working health probes, an OpenAPI document and Swagger UI, correlate and time every request,
/// and translate the FactoryOS domain-exception family into RFC 7807 problem-details responses.
/// </summary>
public sealed class HostFoundationTests
{
    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenApi:Title"] = "FactoryOS Test API",
            ["ApiVersioning:DefaultVersion"] = "2.3",
            ["Localization:DefaultCulture"] = "en",
        });

        builder.AddApiHostFoundation();

        var app = builder.Build();
        app.UseApiHostFoundation();
        app.MapApiHostFoundation();

        app.MapGet("/throw/validation", void () =>
            throw new ValidationException(["Name is required", "Amount must be positive"]));
        app.MapGet("/throw/notfound", void () => throw new NotFoundException("Widget '7' was not found."));
        app.MapGet("/throw/unexpected", void () => throw new InvalidOperationException("boom"));

        await app.StartAsync();
        return app;
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_probes_report_healthy(string path)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_openapi_document_is_served()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("3.0.1", json.GetProperty("openapi").GetString());
        Assert.Equal("FactoryOS Test API", json.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("2.3", json.GetProperty("info").GetProperty("version").GetString());
        Assert.True(json.GetProperty("paths").TryGetProperty("/health", out _));
    }

    [Fact]
    public async Task The_swagger_ui_is_served()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/swagger", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.ToString());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("/openapi/v1.json", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_response_carries_a_correlation_id_and_a_timing_header()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.True(response.Headers.Contains(HeaderNames.CorrelationId));
        Assert.True(response.Headers.Contains("X-Response-Time-ms"));
    }

    [Fact]
    public async Task An_inbound_correlation_id_is_echoed_back()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Add(HeaderNames.CorrelationId, "corr-123");
        var response = await client.SendAsync(request);

        Assert.Equal("corr-123", response.Headers.GetValues(HeaderNames.CorrelationId).Single());
    }

    [Fact]
    public async Task A_validation_exception_becomes_a_400_problem_details()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/throw/validation", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Equal(2, problem.GetProperty("errors").GetArrayLength());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task A_not_found_exception_becomes_a_404_problem_details()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/throw/notfound", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("not_found", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_unexpected_exception_becomes_a_500_without_leaking_detail()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri("/throw/unexpected", UriKind.Relative));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(problem.TryGetProperty("code", out _));
        Assert.DoesNotContain("boom", problem.GetProperty("detail").GetString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Configuration_binds_the_foundation_options()
    {
        await using var app = await StartAsync();

        var openApi = app.Services.GetRequiredService<IOptions<OpenApiSettings>>().Value;
        var versioning = app.Services.GetRequiredService<IOptions<ApiVersioningSettings>>().Value;

        Assert.Equal("FactoryOS Test API", openApi.Title);
        Assert.Equal("2.3", versioning.DefaultVersion);
    }
}
