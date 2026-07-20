using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryOS.Api;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Identity.Seeding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FactoryOS.IntegrationTests.Api;

/// <summary>
/// The credential/session HTTP surface end-to-end: a seeded user logs in via <c>POST /auth/login</c> and receives an
/// access token plus a refresh token, then rotates that refresh token via <c>POST /auth/refresh</c> to renew the
/// session. Rotation revokes the presented token, so a replayed refresh token is rejected — proving the SPA can renew
/// a short-lived access token without re-prompting for credentials, and that a leaked older token cannot be reused.
/// </summary>
public sealed class AuthEndpointsTests
{
    private const string Password = "Passw0rd!";

    private static async Task<TestServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "factoryos",
            ["Jwt:Audience"] = "factoryos",
            ["Jwt:SigningKey"] = "0123456789-abcdefghij-ABCDEFGHIJ-key",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
        });

        builder.Services.AddSystemClock();
        builder.Services.AddIdentityModule(builder.Configuration);

        var app = builder.Build();

        // Seed the default roles and one demo user per role so there is a real identity to authenticate.
        app.Services.GetRequiredService<DefaultIdentitySeeder>().Seed(new IdentitySeedOptions { Password = Password });

        app.MapAuthEndpoints();

        await app.StartAsync();
        return app.GetTestServer();
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static object LoginBody() =>
        new { tenantId = IdentitySeedOptions.DefaultTenantId, userName = "admin", password = Password };

    [Fact]
    public async Task Login_returns_an_access_token_a_refresh_token_and_permissions()
    {
        using var server = await StartAsync();
        using var client = server.CreateClient();

        var login = await PostAsync(client, "/auth/login", LoginBody());

        Assert.False(string.IsNullOrWhiteSpace(login.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(login.GetProperty("refreshToken").GetString()));
        Assert.Contains(
            "*",
            login.GetProperty("permissions").EnumerateArray().Select(element => element.GetString()));
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_preserves_the_permissions()
    {
        using var server = await StartAsync();
        using var client = server.CreateClient();

        var login = await PostAsync(client, "/auth/login", LoginBody());
        var firstRefresh = login.GetProperty("refreshToken").GetString();

        var refreshed = await PostAsync(client, "/auth/refresh", new { refreshToken = firstRefresh });
        var secondRefresh = refreshed.GetProperty("refreshToken").GetString();

        Assert.False(string.IsNullOrWhiteSpace(refreshed.GetProperty("accessToken").GetString()));
        Assert.NotEqual(firstRefresh, secondRefresh);
        Assert.Contains(
            "*",
            refreshed.GetProperty("permissions").EnumerateArray().Select(element => element.GetString()));
    }

    [Fact]
    public async Task A_rotated_refresh_token_cannot_be_replayed()
    {
        using var server = await StartAsync();
        using var client = server.CreateClient();

        var login = await PostAsync(client, "/auth/login", LoginBody());
        var firstRefresh = login.GetProperty("refreshToken").GetString();

        // First rotation succeeds and revokes the presented token.
        await PostAsync(client, "/auth/refresh", new { refreshToken = firstRefresh });

        // Replaying the now-revoked token must be rejected.
        var replay = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = firstRefresh });

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task An_unknown_refresh_token_is_rejected()
    {
        using var server = await StartAsync();
        using var client = server.CreateClient();

        var replay = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }
}

/// <summary>Local helper to register the system clock the Identity token services depend on.</summary>
internal static class ClockRegistrationExtensions
{
    public static IServiceCollection AddSystemClock(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        return services;
    }
}
