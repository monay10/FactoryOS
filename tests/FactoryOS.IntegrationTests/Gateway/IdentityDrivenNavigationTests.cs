using System.Security.Claims;
using System.Text.Json;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Gateway.Routing;
using FactoryOS.Gateway.Ui;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Tokens;
using FactoryOS.Plugin.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static FactoryOS.IntegrationTests.Gateway.GatewayFixtures;

namespace FactoryOS.IntegrationTests.Gateway;

/// <summary>
/// The full RBAC chain: the Identity layer issues a signed access token carrying <c>factoryos:permission</c> claims,
/// an edge bridge validates it into the request principal, and the gateway filters navigation to exactly the screens
/// those permissions allow — the gateway never references Identity, only the claim-type convention.
/// </summary>
public sealed class IdentityDrivenNavigationTests
{
    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    }

    private static JwtAccessTokenService TokenService() =>
        new(
            Options.Create(new JwtOptions
            {
                Issuer = "factoryos",
                Audience = "factoryos",
                SigningKey = "0123456789-abcdefghij-ABCDEFGHIJ-key",
                AccessTokenMinutes = 15,
            }),
            new FixedClock());

    private static async Task<WebApplication> StartAsync(IAccessTokenService tokens)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IPluginHost>(new FakePluginHost(
            Module("dashboard", PluginState.Started, Screen("dash.ops", 1, section: "Experience", permission: "dashboard.view")),
            Module("store", PluginState.Started, Screen("store.admin", 1, section: "Admin", permission: "store.manage"))));
        builder.Services.AddSingleton<IModuleUiCatalogProvider, ModuleUiCatalogProvider>();
        builder.Services.AddTenantResolution();
        builder.Services.AddPermissionResolution();

        var app = builder.Build();

        // The edge bridge the Api host installs: validate the Bearer token into the request principal.
        app.Use(async (context, next) =>
        {
            var header = context.Request.Headers.Authorization.ToString();
            if (header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                var validated = tokens.Validate(header["Bearer ".Length..]);
                if (validated.IsSuccess)
                {
                    context.User = validated.Value;
                }
            }

            await next(context);
        });
        app.UsePermissionResolution();
        app.MapModuleGateway();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task An_identity_token_filters_navigation_to_its_permissions()
    {
        var tokens = TokenService();
        await using var app = await StartAsync(tokens);

        // A supervisor whose role grants only dashboard.view — not store.manage.
        var token = tokens.Create([
            new Claim(FactoryClaimTypes.Subject, Guid.NewGuid().ToString()),
            new Claim(FactoryClaimTypes.Permission, "dashboard.view"),
        ]);

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/modules/ui/nav", UriKind.Relative));
        request.Headers.Add("Authorization", $"Bearer {token.Value}");
        using var document = JsonDocument.Parse(await (await app.GetTestClient().SendAsync(request)).Content.ReadAsStringAsync());

        var section = Assert.Single(document.RootElement.GetProperty("sections").EnumerateArray());
        Assert.Equal("Experience", section.GetProperty("section").GetString());
        Assert.Equal("dashboard", Assert.Single(section.GetProperty("items").EnumerateArray()).GetProperty("module").GetString());
    }

    [Fact]
    public async Task An_invalid_token_leaves_the_request_unrestricted()
    {
        await using var app = await StartAsync(TokenService());

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/modules/ui/nav", UriKind.Relative));
        request.Headers.Add("Authorization", "Bearer not.a.valid.token");
        using var document = JsonDocument.Parse(await (await app.GetTestClient().SendAsync(request)).Content.ReadAsStringAsync());

        // The token never became a principal, so no permission set was bound → everything shows.
        Assert.Equal(2, document.RootElement.GetProperty("sections").GetArrayLength());
    }
}
