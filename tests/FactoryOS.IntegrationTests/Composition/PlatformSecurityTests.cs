using FactoryOS.Api.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using SecurityEngine = FactoryOS.Plugins.Workflow.Security.Execution.SecurityEngine;

namespace FactoryOS.IntegrationTests.Composition;

/// <summary>
/// Exercises the security-grants management projection against the real composed security engine. These prove the
/// read projection is tenant-scoped and ordered, and that granting and revoking through the engine facade the
/// endpoints call behaves as the surface promises — without standing up the HTTP pipeline.
/// </summary>
public sealed class PlatformSecurityTests
{
    private const string Tenant = "acme";
    private static readonly string[] OrderedGrants = ["energy.read", "quality.read"];

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddPlatformEngines(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public void A_granted_permission_is_read_back_for_the_subject()
    {
        using var provider = Build();
        var security = provider.GetRequiredService<SecurityEngine>();

        security.Grant(Tenant, "alice", "energy.read", "admin");

        var view = PlatformSecurity.Grants(security, Tenant, "alice");

        Assert.Equal(Tenant, view.Tenant);
        Assert.Equal("alice", view.Subject);
        Assert.Contains("energy.read", view.Grants);
    }

    [Fact]
    public void Grants_are_read_back_ordered()
    {
        using var provider = Build();
        var security = provider.GetRequiredService<SecurityEngine>();

        security.Grant(Tenant, "alice", "quality.read", "admin");
        security.Grant(Tenant, "alice", "energy.read", "admin");

        var grants = PlatformSecurity.Grants(security, Tenant, "alice").Grants;

        Assert.Equal(OrderedGrants, grants);
    }

    [Fact]
    public void A_grant_never_leaks_across_tenants()
    {
        using var provider = Build();
        var security = provider.GetRequiredService<SecurityEngine>();

        security.Grant(Tenant, "alice", "energy.read", "admin");

        Assert.Empty(PlatformSecurity.Grants(security, "other-factory", "alice").Grants);
    }

    [Fact]
    public void Revoking_reports_whether_the_subject_held_the_permission()
    {
        using var provider = Build();
        var security = provider.GetRequiredService<SecurityEngine>();

        security.Grant(Tenant, "alice", "energy.read", "admin");

        // Revoking a held permission reports it was there and removes it; revoking it again reports it was not.
        Assert.True(security.RevokePermission(Tenant, "alice", "energy.read", "admin"));
        Assert.Empty(PlatformSecurity.Grants(security, Tenant, "alice").Grants);
        Assert.False(security.RevokePermission(Tenant, "alice", "energy.read", "admin"));
    }
}
