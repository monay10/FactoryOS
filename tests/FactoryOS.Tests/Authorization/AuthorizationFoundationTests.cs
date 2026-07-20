using FactoryOS.Domain.Abstractions;
using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Authorization.Caching;
using FactoryOS.Identity.Authorization.Configuration;
using FactoryOS.Identity.Authorization.Context;
using FactoryOS.Identity.Authorization.Evaluation;
using FactoryOS.Identity.Authorization.Handlers;
using FactoryOS.Identity.Authorization.Model;
using FactoryOS.Identity.Authorization.Services;
using FactoryOS.Tests.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AuthorizationOptions = FactoryOS.Identity.Authorization.Configuration.AuthorizationOptions;

namespace FactoryOS.Tests.Authorization;

public sealed class AuthorizationFoundationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 20, 12, 00, 00, TimeSpan.Zero);

    private static IOptions<AuthorizationOptions> Options(Action<AuthorizationOptions>? configure = null)
    {
        var options = new AuthorizationOptions();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    private static InMemoryAuthorizationCache Cache(Action<AuthorizationOptions>? configure = null) =>
        new(new MutableClock(Now), Options(configure));

    // ---- Permission evaluation (wildcard + hierarchical) ----------------------

    [Theory]
    [InlineData("energy.read", "energy.read", true)]     // exact
    [InlineData("energy.*", "energy.read", true)]        // trailing wildcard
    [InlineData("energy.*", "energy.read.detail", true)] // wildcard consumes the remainder
    [InlineData("energy", "energy.read", true)]          // hierarchical prefix grants descendant
    [InlineData("*", "anything.at.all", true)]           // super-admin
    [InlineData("energy.read", "energy.write", false)]   // different action
    [InlineData("energy.read", "energy", false)]         // a specific grant never covers a broader node
    [InlineData("quality.*", "energy.read", false)]      // different resource
    public void PermissionEvaluator_honours_wildcard_and_hierarchy(string granted, string required, bool expected)
    {
        Assert.Equal(expected, new PermissionEvaluator().Grants(granted, required));
    }

    [Fact]
    public void PermissionEvaluator_evaluates_a_grant_set()
    {
        var evaluator = new PermissionEvaluator();
        var granted = new[] { "dashboard.view", "energy.*" };

        Assert.True(evaluator.Evaluate(granted, "energy.read"));
        Assert.False(evaluator.Evaluate(granted, "quality.read"));
    }

    // ---- Role inheritance -----------------------------------------------------

    [Fact]
    public void RoleService_resolves_transitive_inheritance()
    {
        var roles = new RoleService(Cache(), Options());
        roles.AddParent("Supervisor", "Operator");
        roles.AddParent("Operator", "Viewer");

        var effective = roles.GetEffectiveRoles("Supervisor");

        Assert.Contains("Supervisor", effective);
        Assert.Contains("Operator", effective);
        Assert.Contains("Viewer", effective);
        Assert.True(roles.IsInRole(["Supervisor"], "Viewer"));
        Assert.False(roles.IsInRole(["Viewer"], "Supervisor"));
    }

    [Fact]
    public void RoleService_is_cycle_safe()
    {
        var roles = new RoleService(Cache(), Options());
        roles.AddParent("A", "B");
        roles.AddParent("B", "A");

        Assert.Equal(2, roles.GetEffectiveRoles("A").Count);
    }

    [Fact]
    public void RoleService_can_disable_inheritance()
    {
        var roles = new RoleService(Cache(), Options(o => o.EnableRoleInheritance = false));
        roles.AddParent("Supervisor", "Operator");

        Assert.Single(roles.GetEffectiveRoles("Supervisor"));
        Assert.False(roles.IsInRole(["Supervisor"], "Operator"));
    }

    // ---- Caching --------------------------------------------------------------

    [Fact]
    public void Cache_serves_within_the_ttl_and_recomputes_after_it_expires()
    {
        var clock = new MutableClock(Now);
        var cache = new InMemoryAuthorizationCache(clock, Options(o => o.PermissionCache.TtlSeconds = 60));
        var calls = 0;
        int Factory() => ++calls;

        Assert.Equal(1, cache.GetOrAdd("k", Factory));
        Assert.Equal(1, cache.GetOrAdd("k", Factory)); // served from cache

        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(2, cache.GetOrAdd("k", Factory)); // recomputed after expiry
    }

    [Fact]
    public void Cache_is_bypassed_when_disabled_and_invalidation_forces_recompute()
    {
        var disabled = new InMemoryAuthorizationCache(new MutableClock(Now), Options(o => o.PermissionCache.Enabled = false));
        var calls = 0;
        Assert.Equal(1, disabled.GetOrAdd("k", () => ++calls));
        Assert.Equal(2, disabled.GetOrAdd("k", () => ++calls));

        var cache = Cache();
        var hits = 0;
        cache.GetOrAdd("k", () => ++hits);
        cache.Invalidate("k");
        cache.GetOrAdd("k", () => ++hits);
        Assert.Equal(2, hits);
    }

    // ---- Permission service (resolution) --------------------------------------

    [Fact]
    public void PermissionService_resolves_role_permissions_through_inheritance()
    {
        var roles = new RoleService(Cache(), Options());
        roles.AddParent("Supervisor", "Viewer");
        var permissions = new PermissionService(roles, Cache());
        permissions.GrantToRole("Viewer", "dashboard.view");
        permissions.GrantToRole("Supervisor", "oee.view");

        var resolved = permissions.ResolveRolePermissions("Supervisor");

        Assert.Contains("dashboard.view", resolved);
        Assert.Contains("oee.view", resolved);
    }

    [Fact]
    public void PermissionService_unions_user_grants_and_subtracts_denials()
    {
        var roles = new RoleService(Cache(), Options());
        var permissions = new PermissionService(roles, Cache());
        var user = Guid.NewGuid();
        permissions.GrantToRole("Viewer", "dashboard.view");
        permissions.GrantToRole("Viewer", "oee.view");
        permissions.GrantToUser(user, "energy.read");
        permissions.DenyToUser(user, "oee.view");

        var resolved = permissions.ResolveEffectivePermissions(user, ["Viewer"]);

        Assert.Contains("dashboard.view", resolved);
        Assert.Contains("energy.read", resolved);
        Assert.DoesNotContain("oee.view", resolved); // explicit user denial wins over the role grant
    }

    [Fact]
    public void PermissionService_exposes_the_catalog()
    {
        var permissions = new PermissionService(new RoleService(Cache(), Options()), Cache());
        permissions.DefineGroup(new PermissionGroup("energy", "Energy"));
        permissions.Define(new PermissionDefinition("energy.read", "Read energy", "energy"));

        Assert.Single(permissions.GetGroups());
        Assert.Single(permissions.GetDefinitions());
    }

    // ---- Policy provider & handlers -------------------------------------------

    [Fact]
    public void PolicyProvider_seeds_from_configuration_and_accepts_runtime_policies()
    {
        var provider = new PolicyProvider(Options(o =>
        {
            var settings = new PolicySettings { RequireAll = false };
            settings.Permissions.Add("energy.read");
            settings.Permissions.Add("energy.write");
            o.Policies["EnergyAccess"] = settings;
        }));

        var seeded = provider.GetPolicy("EnergyAccess");
        Assert.NotNull(seeded);
        Assert.False(seeded!.RequireAll);
        Assert.Equal(2, seeded.Permissions.Count);

        provider.AddPolicy(AuthorizationPolicy.RequireAllOf("Admin", "*"));
        Assert.NotNull(provider.GetPolicy("Admin"));
        Assert.Equal(2, provider.GetPolicies().Count);
    }

    [Fact]
    public void Handlers_evaluate_their_requirement_kinds()
    {
        var roles = new RoleService(Cache(), Options());
        roles.AddParent("Supervisor", "Viewer");
        var evaluator = new PermissionEvaluator();
        var provider = new PolicyProvider(Options());
        provider.AddPolicy(AuthorizationPolicy.RequireAllOf("Energy", "energy.read"));

        var context = new AuthorizationContext(
            Guid.NewGuid(), Guid.NewGuid(), ["Supervisor"], ["energy.*"]);

        var permission = new PermissionAuthorizationHandler(evaluator);
        Assert.True(permission.CanHandle(new PermissionRequirement("energy.read")));
        Assert.True(permission.Handle(context, new PermissionRequirement("energy.read")).Succeeded);
        Assert.False(permission.Handle(context, new PermissionRequirement("quality.read")).Succeeded);

        var role = new RoleAuthorizationHandler(roles);
        Assert.True(role.Handle(context, new RoleRequirement("Viewer")).Succeeded); // via inheritance
        Assert.False(role.Handle(context, new RoleRequirement("Administrator")).Succeeded);

        var policy = new PolicyAuthorizationHandler(provider, evaluator);
        Assert.True(policy.Handle(context, new PolicyRequirement("Energy")).Succeeded);
        Assert.False(policy.Handle(context, new PolicyRequirement("Missing")).Succeeded);
    }

    // ---- Authorization service dispatch ---------------------------------------

    [Fact]
    public void AuthorizationService_dispatches_to_the_matching_handler()
    {
        var roles = new RoleService(Cache(), Options());
        var evaluator = new PermissionEvaluator();
        var provider = new PolicyProvider(Options());
        var service = new AuthorizationService(
        [
            new PermissionAuthorizationHandler(evaluator),
            new RoleAuthorizationHandler(roles),
            new PolicyAuthorizationHandler(provider, evaluator),
        ]);

        var context = new AuthorizationContext(Guid.NewGuid(), Guid.NewGuid(), ["Operator"], ["energy.read"]);

        Assert.True(service.IsGranted(context, "energy.read"));
        Assert.False(service.IsGranted(context, "energy.write"));
        Assert.True(service.Authorize(context, new RoleRequirement("Operator")).Succeeded);
    }

    // ---- Dependency injection & context accessor ------------------------------

    [Fact]
    public void AddAuthorizationFoundation_registers_and_resolves_the_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:PermissionCache:TtlSeconds"] = "120",
                ["Authorization:Policies:EnergyAccess:RequireAll"] = "true",
                ["Authorization:Policies:EnergyAccess:Permissions:0"] = "energy.read",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(new MutableClock(Now));
        services.AddAuthorizationFoundation(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.IsType<PermissionEvaluator>(sp.GetRequiredService<IPermissionEvaluator>());
        Assert.IsType<RoleService>(sp.GetRequiredService<IRoleService>());
        Assert.IsType<PermissionService>(sp.GetRequiredService<IPermissionService>());
        Assert.IsType<PolicyProvider>(sp.GetRequiredService<IPolicyProvider>());
        Assert.IsType<AuthorizationService>(sp.GetRequiredService<IAuthorizationService>());
        Assert.IsType<AuthorizationContextAccessor>(sp.GetRequiredService<IAuthorizationContextAccessor>());
        Assert.Equal(3, sp.GetServices<IAuthorizationHandler>().Count());

        // The configured policy was seeded.
        Assert.NotNull(sp.GetRequiredService<IPolicyProvider>().GetPolicy("EnergyAccess"));
    }
}
