using System.Net;
using System.Text.Json;
using FactoryOS.Gateway.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FactoryOS.IntegrationTests.Gateway;

/// <summary>
/// The gateway resolves each request's tenant once, at the edge, into the scoped <see cref="ITenantContext"/>.
/// A probe endpoint reads the ambient tenant instead of parsing a query parameter, proving that a module
/// endpoint no longer re-validates the tenant on every route. Multi-tenancy stays a construction rule: the
/// header name is configuration, never hard-coded, and requests with no tenant are left for the endpoint to
/// reject.
/// </summary>
public sealed class TenantResolutionTests
{
    private static async Task<WebApplication> StartAsync(Action<TenantResolutionOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddTenantResolution(configure);

        var app = builder.Build();
        app.UseTenantResolution();
        app.MapGet("/probe", (ITenantContext tenant) =>
            Results.Ok(new { resolved = tenant.TryGetTenant(out var value), tenant = value }));
        app.MapGet("/guarded", (ITenantContext tenant) => Results.Ok(new { tenant = tenant.Tenant }))
            .RequireTenant();

        await app.StartAsync();
        return app;
    }

    private static async Task<(bool Resolved, string? Tenant)> ProbeAsync(WebApplication app, HttpRequestMessage request)
    {
        var response = await app.GetTestClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var tenant = document.RootElement.GetProperty("tenant");
        return (
            document.RootElement.GetProperty("resolved").GetBoolean(),
            tenant.ValueKind == JsonValueKind.Null ? null : tenant.GetString());
    }

    private static HttpRequestMessage Get(string path) =>
        new(HttpMethod.Get, new Uri(path, UriKind.Relative));

    [Fact]
    public async Task Resolves_the_tenant_from_the_header()
    {
        await using var app = await StartAsync();

        var request = Get("/probe");
        request.Headers.Add("X-FactoryOS-Tenant", "acme");

        Assert.Equal((true, "acme"), await ProbeAsync(app, request));
    }

    [Fact]
    public async Task Falls_back_to_the_query_string()
    {
        await using var app = await StartAsync();

        Assert.Equal((true, "globex"), await ProbeAsync(app, Get("/probe?tenant=globex")));
    }

    [Fact]
    public async Task Prefers_the_header_over_the_query_string()
    {
        await using var app = await StartAsync();

        var request = Get("/probe?tenant=globex");
        request.Headers.Add("X-FactoryOS-Tenant", "acme");

        Assert.Equal((true, "acme"), await ProbeAsync(app, request));
    }

    [Fact]
    public async Task Ignores_a_blank_tenant()
    {
        await using var app = await StartAsync();

        var request = Get("/probe?tenant=%20");
        request.Headers.Add("X-FactoryOS-Tenant", "   ");

        Assert.Equal((false, (string?)null), await ProbeAsync(app, request));
    }

    [Fact]
    public async Task RequireTenant_rejects_a_request_without_a_tenant()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync(new Uri("/guarded", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequireTenant_admits_a_request_with_a_tenant()
    {
        await using var app = await StartAsync();

        var request = Get("/guarded");
        request.Headers.Add("X-FactoryOS-Tenant", "acme");
        var response = await app.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("acme", document.RootElement.GetProperty("tenant").GetString());
    }

    [Fact]
    public async Task Honors_a_configured_header_name()
    {
        await using var app = await StartAsync(options => options.HeaderName = "X-Factory");

        var request = Get("/probe");
        request.Headers.Add("X-Factory", "initech");

        Assert.Equal((true, "initech"), await ProbeAsync(app, request));
    }
}
