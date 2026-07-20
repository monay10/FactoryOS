using System.Security.Claims;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Configuration;
using FactoryOS.Identity.Context;
using FactoryOS.Identity.Credentials;
using FactoryOS.Identity.Domain;
using FactoryOS.Identity.Execution;
using FactoryOS.Identity.Lockout;
using FactoryOS.Identity.Persistence;
using FactoryOS.Identity.Policies;
using FactoryOS.Identity.Services;
using FactoryOS.Identity.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Identity;

public sealed class IdentityFoundationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 20, 12, 00, 00, TimeSpan.Zero);

    private static IOptions<IdentityOptions> Options(Action<IdentityOptions>? configure = null)
    {
        var options = new IdentityOptions();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    // ---- Dependency injection -------------------------------------------------

    [Fact]
    public void AddIdentityFoundation_registers_and_resolves_the_foundation_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "integration-signing-key-that-is-long-enough-32b",
                ["Identity:PasswordPolicy:MinimumLength"] = "10",
                ["Identity:Session:IdleTimeoutMinutes"] = "20",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(new MutableClock(Now));
        services.AddIdentityFoundation(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IdentityContext>());
        Assert.IsType<CurrentPrincipalAccessor>(sp.GetRequiredService<ICurrentPrincipalAccessor>());
        Assert.IsType<CurrentClaimsAccessor>(sp.GetRequiredService<ICurrentClaimsAccessor>());
        Assert.IsType<PasswordPolicyValidator>(sp.GetRequiredService<IPasswordPolicy>());
        Assert.IsType<AccountLockoutService>(sp.GetRequiredService<IAccountLockoutService>());
        Assert.IsType<SessionService>(sp.GetRequiredService<ISessionService>());
        Assert.IsType<IdentityService>(sp.GetRequiredService<IIdentityService>());

        // The bound configuration flows through to the options.
        Assert.Equal(10, sp.GetRequiredService<IOptions<IdentityOptions>>().Value.PasswordPolicy.MinimumLength);
    }

    // ---- Password policy ------------------------------------------------------

    [Theory]
    [InlineData("short1!", false)]        // too short
    [InlineData("alllower1!", false)]     // no uppercase
    [InlineData("ALLUPPER1!", false)]     // no lowercase
    [InlineData("NoDigits!!", false)]     // no digit
    [InlineData("NoSpecial1", false)]     // no non-alphanumeric
    [InlineData("Str0ng!Pass", true)]     // satisfies all
    public void PasswordPolicy_enforces_length_and_character_classes(string password, bool expected)
    {
        var policy = new PasswordPolicyValidator(Options());

        Assert.Equal(expected, policy.Validate(password).IsSuccess);
    }

    [Fact]
    public void PasswordPolicy_can_relax_requirements()
    {
        var policy = new PasswordPolicyValidator(Options(o =>
        {
            o.PasswordPolicy.RequireNonAlphanumeric = false;
            o.PasswordPolicy.RequireUppercase = false;
        }));

        Assert.True(policy.Validate("lowercase1").IsSuccess);
    }

    // ---- Account lockout ------------------------------------------------------

    [Fact]
    public void Lockout_locks_after_the_threshold_and_a_success_resets_it()
    {
        var clock = new MutableClock(Now);
        var service = new AccountLockoutService(
            new InMemoryLoginAttemptStore(), clock, Options(o =>
            {
                o.Lockout.MaxFailedAccessAttempts = 3;
                o.Lockout.LockoutMinutes = 15;
            }));
        var user = Guid.NewGuid();

        service.RecordFailure(user);
        service.RecordFailure(user);
        Assert.False(service.IsLockedOut(user));

        var state = service.RecordFailure(user);
        Assert.Equal(3, state.FailedAttempts);
        Assert.True(service.IsLockedOut(user));

        // The lockout ends once the window elapses.
        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.False(service.IsLockedOut(user));

        // A success clears accumulated failures, so counting restarts from one.
        service.RecordFailure(user);
        service.RecordSuccess(user);
        Assert.Equal(1, service.RecordFailure(user).FailedAttempts);
    }

    [Fact]
    public void Lockout_is_inert_when_disabled()
    {
        var service = new AccountLockoutService(
            new InMemoryLoginAttemptStore(), new MutableClock(Now), Options(o => o.Lockout.Enabled = false));
        var user = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            service.RecordFailure(user);
        }

        Assert.False(service.IsLockedOut(user));
    }

    // ---- Session lifecycle ----------------------------------------------------

    [Fact]
    public void Session_is_created_active_and_revocation_deactivates_it()
    {
        var clock = new MutableClock(Now);
        var service = new SessionService(new InMemorySessionStore(), clock, Options());
        var session = service.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(service.Validate(session.Id).IsSuccess);

        service.Revoke(session.Id);
        Assert.True(service.Validate(session.Id).IsFailure);
    }

    [Fact]
    public void Session_expires_when_idle_and_touch_slides_the_window()
    {
        var clock = new MutableClock(Now);
        var service = new SessionService(new InMemorySessionStore(), clock, Options(o =>
        {
            o.Session.IdleTimeoutMinutes = 30;
            o.Session.AbsoluteTimeoutHours = 8;
            o.Session.SlidingExpiration = true;
        }));
        var session = service.Create(Guid.NewGuid(), Guid.NewGuid());

        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.True(service.Touch(session.Id).IsSuccess); // slides idle to now+30

        clock.Advance(TimeSpan.FromMinutes(25)); // 45 min after create, but only 25 since touch
        Assert.True(service.Validate(session.Id).IsSuccess);

        clock.Advance(TimeSpan.FromMinutes(31)); // now idle window elapsed
        Assert.True(service.Validate(session.Id).IsFailure);
    }

    [Fact]
    public void Session_honours_the_absolute_timeout_even_when_touched()
    {
        var clock = new MutableClock(Now);
        var service = new SessionService(new InMemorySessionStore(), clock, Options(o =>
        {
            o.Session.IdleTimeoutMinutes = 30;
            o.Session.AbsoluteTimeoutHours = 1;
        }));
        var session = service.Create(Guid.NewGuid(), Guid.NewGuid());

        clock.Advance(TimeSpan.FromMinutes(50));
        service.Touch(session.Id); // idle would slide to +30, but is capped at the absolute expiry

        clock.Advance(TimeSpan.FromMinutes(15)); // 65 min after create > 60 min absolute
        Assert.True(service.Validate(session.Id).IsFailure);
    }

    [Fact]
    public void RevokeAllForUser_revokes_every_live_session()
    {
        var clock = new MutableClock(Now);
        var service = new SessionService(new InMemorySessionStore(), clock, Options());
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var a = service.Create(user, tenant);
        var b = service.Create(user, tenant);

        Assert.Equal(2, service.RevokeAllForUser(user));
        Assert.True(service.Validate(a.Id).IsFailure);
        Assert.True(service.Validate(b.Id).IsFailure);
        Assert.Equal(0, service.RevokeAllForUser(user));
    }

    // ---- Identity service: registration & resolution --------------------------

    private static IdentityService NewIdentityService(
        IUserStore users, IRoleStore roles, Action<IdentityOptions>? configure = null) =>
        new(users, roles, new Pbkdf2PasswordHasher(), new PasswordPolicyValidator(Options(configure)), Options(configure));

    [Fact]
    public void RegisterUser_rejects_a_weak_password()
    {
        var service = NewIdentityService(new InMemoryUserStore(), new InMemoryRoleStore());

        var result = service.RegisterUser(Guid.NewGuid(), "alice", "alice@factoryos.local", "weak");

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.Password.TooShort", result.Error.Code);
    }

    [Fact]
    public void RegisterUser_rejects_a_duplicate_user_name_in_the_same_tenant()
    {
        var users = new InMemoryUserStore();
        var service = NewIdentityService(users, new InMemoryRoleStore());
        var tenant = Guid.NewGuid();

        Assert.True(service.RegisterUser(tenant, "alice", "alice@factoryos.local", "Str0ng!Pass").IsSuccess);
        var duplicate = service.RegisterUser(tenant, "alice", "other@factoryos.local", "Str0ng!Pass");

        Assert.True(duplicate.IsFailure);
        Assert.Equal("Identity.User.Duplicate", duplicate.Error.Code);
    }

    [Fact]
    public void RegisterUser_hashes_the_password_and_stores_the_user()
    {
        var users = new InMemoryUserStore();
        var service = NewIdentityService(users, new InMemoryRoleStore());
        var tenant = Guid.NewGuid();

        var result = service.RegisterUser(tenant, "alice", "alice@factoryos.local", "Str0ng!Pass");

        Assert.True(result.IsSuccess);
        Assert.NotEqual("Str0ng!Pass", result.Value.PasswordHash);
        Assert.True(new Pbkdf2PasswordHasher().Verify("Str0ng!Pass", result.Value.PasswordHash));
        Assert.NotNull(users.FindByUserName(tenant, "alice"));
    }

    [Fact]
    public void ChangePassword_validates_the_new_value_against_the_policy()
    {
        var service = NewIdentityService(new InMemoryUserStore(), new InMemoryRoleStore());
        var user = User.Create(Guid.NewGuid(), Guid.NewGuid(), "bob", "bob@factoryos.local", "hash");

        Assert.True(service.ChangePassword(user, "weak").IsFailure);
        Assert.True(service.ChangePassword(user, "Str0ng!Pass").IsSuccess);
        Assert.True(new Pbkdf2PasswordHasher().Verify("Str0ng!Pass", user.PasswordHash));
    }

    [Fact]
    public void Resolve_returns_the_effective_roles_permissions_and_claims()
    {
        var users = new InMemoryUserStore();
        var roles = new InMemoryRoleStore();
        var tenant = Guid.NewGuid();

        var operatorRole = Role.Create(Guid.NewGuid(), tenant, "Operator");
        operatorRole.Grant(Permission.Parse("energy.*"));
        operatorRole.Grant(Permission.Parse("dashboard.view"));
        var viewerRole = Role.Create(Guid.NewGuid(), tenant, "Viewer");
        viewerRole.Grant(Permission.Parse("dashboard.view")); // overlaps → de-duplicated
        roles.Add(operatorRole);
        roles.Add(viewerRole);

        var user = User.Create(Guid.NewGuid(), tenant, "carol", "carol@factoryos.local", "hash");
        user.AssignRole(operatorRole.Id);
        user.AssignRole(viewerRole.Id);
        users.Add(user);

        var service = NewIdentityService(users, roles);

        Assert.Equal(2, service.ResolveRoles(user).Count);
        Assert.Equal(2, service.ResolvePermissions(user).Count); // energy.* + dashboard.view
        var claims = service.ResolveClaims(user);
        Assert.Contains(claims, c => c.Type == FactoryClaimTypes.Role && c.Value == "Operator");
        Assert.Contains(claims, c => c.Type == FactoryClaimTypes.Permission && c.Value == "energy.*");
        Assert.Equal(user.Id.ToString(), claims.First(c => c.Type == FactoryClaimTypes.Subject).Value);
    }

    // ---- Identity context & current accessors ---------------------------------

    [Fact]
    public void IdentityContext_can_be_initialized_only_once()
    {
        var context = new IdentityContext();
        Assert.False(context.IsAuthenticated);

        context.Initialize(Principal());

        Assert.True(context.IsAuthenticated);
        Assert.Throws<InvalidOperationException>(() => context.Initialize(Principal()));
    }

    [Fact]
    public void Current_accessors_read_the_principal_and_its_claims()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var context = new IdentityContext();
        context.Initialize(Principal(userId, tenantId, "session-123", "energy.read"));

        var principal = new CurrentPrincipalAccessor(context);
        Assert.True(principal.IsAuthenticated);
        Assert.Equal(userId, principal.UserId);
        Assert.Equal(tenantId, principal.TenantId);
        Assert.Equal("session-123", principal.SessionId);

        var claims = new CurrentClaimsAccessor(context);
        Assert.Equal("session-123", claims.Find(FactoryClaimTypes.Session));
        Assert.Contains("energy.read", claims.FindAll(FactoryClaimTypes.Permission));
        Assert.True(claims.Has(FactoryClaimTypes.Tenant, tenantId.ToString()));
    }

    private static ClaimsPrincipal Principal(
        Guid? userId = null, Guid? tenantId = null, string? sessionId = null, string? permission = null)
    {
        var claims = new List<Claim>
        {
            new(FactoryClaimTypes.Subject, (userId ?? Guid.NewGuid()).ToString()),
            new(FactoryClaimTypes.Tenant, (tenantId ?? Guid.NewGuid()).ToString()),
        };
        if (sessionId is not null)
        {
            claims.Add(new Claim(FactoryClaimTypes.Session, sessionId));
        }

        if (permission is not null)
        {
            claims.Add(new Claim(FactoryClaimTypes.Permission, permission));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
