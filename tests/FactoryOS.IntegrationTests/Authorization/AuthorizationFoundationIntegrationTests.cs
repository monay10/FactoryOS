using System.Security.Claims;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Authorization.Context;
using FactoryOS.Identity.Authorization.Model;
using FactoryOS.Identity.Authorization.Services;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Context;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Authorization;

/// <summary>
/// The authorization foundation composed through <c>AddAuthorizationFoundation</c> against a real container:
/// a principal's claims are mapped into an authorization context, a configured policy is enforced, role
/// inheritance is honoured end-to-end, and a wildcard permission grant satisfies a hierarchical requirement.
/// </summary>
public sealed class AuthorizationFoundationIntegrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:Policies:EnergyAccess:RequireAll"] = "true",
                ["Authorization:Policies:EnergyAccess:Permissions:0"] = "energy.read",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddAuthorizationFoundation(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IServiceScope ScopeWithPrincipal(ServiceProvider provider, params Claim[] claims)
    {
        var scope = provider.CreateScope();
        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        scope.ServiceProvider.GetRequiredService<IdentityContext>().Initialize(new ClaimsPrincipal(identity));
        return scope;
    }

    [Fact]
    public void The_pipeline_maps_claims_and_enforces_permissions_policies_and_roles()
    {
        using var provider = BuildProvider();

        // The role graph is part of the foundation state; declare an inheritance edge.
        provider.GetRequiredService<IRoleService>().AddParent("Supervisor", "Viewer");

        using var scope = ScopeWithPrincipal(
            provider,
            new Claim(FactoryClaimTypes.Subject, Guid.NewGuid().ToString()),
            new Claim(FactoryClaimTypes.Tenant, Guid.NewGuid().ToString()),
            new Claim(FactoryClaimTypes.Role, "Supervisor"),
            new Claim(FactoryClaimTypes.Permission, "energy.*"));
        var sp = scope.ServiceProvider;

        var context = sp.GetRequiredService<IAuthorizationContextAccessor>().Current;
        var authorization = sp.GetRequiredService<IAuthorizationService>();

        Assert.True(context.IsAuthenticated);

        // Wildcard grant satisfies a hierarchical permission requirement.
        Assert.True(authorization.IsGranted(context, "energy.read"));
        Assert.False(authorization.IsGranted(context, "quality.read"));

        // The configured policy is enforced.
        Assert.True(authorization.Authorize(context, "EnergyAccess").Succeeded);

        // Role inheritance: Supervisor inherits Viewer.
        Assert.True(authorization.Authorize(context, new RoleRequirement("Viewer")).Succeeded);
        Assert.False(authorization.Authorize(context, new RoleRequirement("Administrator")).Succeeded);
    }

    [Fact]
    public void An_anonymous_principal_yields_an_anonymous_context()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var context = sp.GetRequiredService<IAuthorizationContextAccessor>().Current;

        Assert.False(context.IsAuthenticated);
        Assert.False(sp.GetRequiredService<IAuthorizationService>().IsGranted(context, "energy.read"));
    }
}
