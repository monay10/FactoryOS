using FactoryOS.Plugins.Workflow.Security.Configuration;
using FactoryOS.Plugins.Workflow.Security.Diagnostics;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Events;
using FactoryOS.Plugins.Workflow.Security.Execution;
using FactoryOS.Plugins.Workflow.Security.Persistence;
using FactoryOS.Plugins.Workflow.Security.Policies;
using FactoryOS.Tests.Identity;

namespace FactoryOS.Tests.Workflow.Security;

/// <summary>
/// Unit coverage of the security engine core: permission and policy evaluation, claims, roles, constraints,
/// rules, sessions, tokens, authorization and correlation — exercised directly, without a container and
/// without any of the engines it protects.
/// </summary>
public sealed class SecurityEngineCoreTests
{
    private const string Tenant = "acme";
    private static readonly DateTimeOffset Now = new(2026, 07, 22, 09, 00, 00, TimeSpan.Zero);

    // ---- Permission evaluation ---------------------------------------------------------------------------------

    [Fact]
    public void A_principal_holds_what_it_was_granted_directly()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Grant(Tenant, "u-ada", SecurityPermissions.Workflow.Start, "u-root");

        Assert.True(harness.Engine.HasPermission(User("u-ada"), SecurityPermissions.Workflow.Start));
        Assert.False(harness.Engine.HasPermission(User("u-ada"), SecurityPermissions.Workflow.Cancel));
    }

    [Fact]
    public void A_wildcard_widens_a_grant_and_a_bare_star_covers_everything()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Grant(Tenant, "u-ada", "workflow.*", "u-root");
        harness.Engine.Grant(Tenant, "u-root", SecurityPermissions.All, "u-root");

        Assert.True(harness.Engine.HasPermission(User("u-ada"), SecurityPermissions.Workflow.Cancel));
        Assert.False(harness.Engine.HasPermission(User("u-ada"), SecurityPermissions.Audit.Export));
        Assert.True(harness.Engine.HasPermission(User("u-root"), SecurityPermissions.Audit.Export));
    }

    [Fact]
    public void An_unauthenticated_principal_holds_nothing_whatever_it_claims()
    {
        var harness = Harness.Create(Now);

        // The claims say the permission is held; nothing established the principal, so it is not.
        var impostor = new SecurityPrincipal(
            "u-nobody",
            Tenant,
            SecurityIdentity.Anonymous,
            [SecurityClaim.Of(SecurityClaim.PermissionType, SecurityPermissions.All)]);

        Assert.False(harness.Engine.HasPermission(impostor, SecurityPermissions.Workflow.Start));
    }

    [Fact]
    public void A_grant_made_in_one_tenant_is_invisible_in_another()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Grant(Tenant, "u-ada", SecurityPermissions.Workflow.Start, "u-root");

        var elsewhere = new SecurityPrincipal("u-ada", "globex", Authenticated);

        Assert.False(harness.Engine.HasPermission(elsewhere, SecurityPermissions.Workflow.Start));
    }

    [Fact]
    public void A_malformed_permission_is_refused_where_somebody_can_still_fix_it()
    {
        var harness = Harness.Create(Now);

        // Refused on the way in rather than silently never matching at the moment it is relied on.
        Assert.Throws<FormatException>(
            () => harness.Engine.Grant(Tenant, "u-ada", "workflow start", "u-root"));
    }

    // ---- Roles -------------------------------------------------------------------------------------------------

    [Fact]
    public void A_role_carries_its_permissions_to_whoever_holds_it()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRole(SecurityRole.Of(
            "operator", "Operator", SecurityPermissions.Workflow.Read, SecurityPermissions.Workflow.Start));

        Assert.True(harness.Engine.HasPermission(
            User("u-ada", "operator"), SecurityPermissions.Workflow.Start));
    }

    [Fact]
    public void An_included_role_carries_everything_it_includes()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRole(SecurityRole.Of("operator", "Operator", SecurityPermissions.Workflow.Start));
        harness.Engine.RegisterRole(
            SecurityRole.Of("supervisor", "Supervisor", SecurityPermissions.Approval.Decide) with
            {
                Includes = ["operator"],
            });

        var supervisor = User("u-bob", "supervisor");

        Assert.True(harness.Engine.HasPermission(supervisor, SecurityPermissions.Approval.Decide));
        Assert.True(harness.Engine.HasPermission(supervisor, SecurityPermissions.Workflow.Start));
    }

    [Fact]
    public void A_role_graph_that_loops_resolves_instead_of_hanging()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRole(
            SecurityRole.Of("a", "A", "workflow.read") with { Includes = ["b"] });
        harness.Engine.RegisterRole(
            SecurityRole.Of("b", "B", "forms.read") with { Includes = ["a"] });

        // A configuration mistake must not be able to take authorization down.
        var permissions = harness.Engine.EffectivePermissions(User("u-ada", "a"));

        Assert.Contains("workflow.read", permissions);
        Assert.Contains("forms.read", permissions);
    }

    [Fact]
    public void A_role_nobody_registered_grants_nothing()
    {
        var harness = Harness.Create(Now);

        // The safe direction: an unknown role granting nothing is a visible gap.
        Assert.Empty(harness.Engine.EffectivePermissions(User("u-ada", "role-that-does-not-exist")));
    }

    // ---- Claims ------------------------------------------------------------------------------------------------

    [Fact]
    public void Resolution_gathers_roles_grants_and_inherited_roles_into_one_principal()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRole(SecurityRole.Of("operator", "Operator", "workflow.start"));
        harness.Engine.RegisterRole(
            SecurityRole.Of("supervisor", "Supervisor", "approval.decide") with { Includes = ["operator"] });
        harness.Engine.Grant(Tenant, "u-bob", "audit.read", "u-root");

        var resolved = harness.Claims.Resolve(User("u-bob", "supervisor"));

        Assert.Contains("operator", resolved.Roles);
        Assert.Contains("supervisor", resolved.Roles);
        Assert.Equal(
            ["approval.decide", "audit.read", "workflow.start"],
            resolved.Permissions.Select(permission => permission.Value).OrderBy(value => value).ToArray());
    }

    [Fact]
    public void Resolving_twice_changes_nothing()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRole(SecurityRole.Of("operator", "Operator", "workflow.start"));

        var once = harness.Claims.Resolve(User("u-ada", "operator"));
        var twice = harness.Claims.Resolve(once);

        // Resolution is a projection, so the same request can safely be resolved on any path it takes.
        Assert.Equal(once.Claims.Count, twice.Claims.Count);
    }

    [Fact]
    public void A_principal_reports_the_claims_it_presented()
    {
        var principal = new SecurityPrincipal(
            "u-ada",
            Tenant,
            Authenticated,
            [SecurityClaim.Of("site", "izmir"), SecurityClaim.Of(SecurityClaim.OrganizationType, "plant-1")]);

        Assert.Equal("izmir", principal.FindFirst("site"));
        Assert.Equal("plant-1", principal.Organization);
        Assert.True(principal.HasClaim("site", "izmir"));
        Assert.False(principal.HasClaim("site", "bursa"));
    }

    // ---- Authorization -----------------------------------------------------------------------------------------

    [Fact]
    public void Nothing_is_permitted_by_default()
    {
        var harness = Harness.Create(Now);

        var decision = harness.Engine.Authorize(User("u-ada"), SecurityPermissions.Workflow.Start);

        Assert.True(decision.IsDenied);
        Assert.Equal(SecurityDecisionReason.NoMatchingRule, decision.Reason);
    }

    [Fact]
    public void An_anonymous_principal_is_refused_before_anything_else_is_asked()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterPolicy(PolicyLibrary.Prohibition("open", "Open", "*"));

        var decision = harness.Engine.Authorize(
            SecurityPrincipal.Anonymous(Tenant), SecurityPermissions.Workflow.Read);

        Assert.Equal(SecurityDecisionReason.NotAuthenticated, decision.Reason);
    }

    [Fact]
    public void A_request_naming_another_tenant_is_refused_structurally()
    {
        var harness = Harness.Create(Now);

        // Even holding everything, and even with a policy that grants everything to everyone.
        harness.Engine.Grant(Tenant, "u-root", SecurityPermissions.All, "u-root");
        harness.Engine.RegisterPolicy(PolicyLibrary.RoleBased("all", "All", "*", "operator"));

        var decision = harness.Engine.Authorize(harness.Engine.Request()
            .For(User("u-root", "operator"))
            .Requesting(SecurityPermissions.Workflow.Start)
            .In(SecurityScope.ForTenant("globex"))
            .Build());

        Assert.Equal(SecurityDecisionReason.TenantMismatch, decision.Reason);
    }

    [Fact]
    public void A_held_permission_grants_and_says_which_one_did_it()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Grant(Tenant, "u-ada", SecurityPermissions.Workflow.Start, "u-root");

        var decision = harness.Engine.Authorize(User("u-ada"), SecurityPermissions.Workflow.Start);

        Assert.True(decision.IsAllowed);
        Assert.Equal(SecurityDecisionReason.GrantedByPermission, decision.Reason);
        Assert.Contains("workflow.start", decision.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void A_denial_names_the_permission_that_was_missing()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterPolicy(PolicyLibrary.RoleBased(
            "starters", "Starters", SecurityPermissions.Workflow.Start, "operator"));

        var decision = harness.Engine.Authorize(User("u-visitor"), SecurityPermissions.Workflow.Start);

        // Somebody thought about this permission, so the denial says "you lack it" rather than "nobody has it".
        Assert.Equal(SecurityDecisionReason.MissingPermission, decision.Reason);
        Assert.Equal("workflow.start", decision.Permission);
    }

    [Fact]
    public void An_explicit_deny_beats_every_grant_there_is()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Grant(Tenant, "u-root", SecurityPermissions.All, "u-root");
        harness.Engine.RegisterPolicy(PolicyLibrary.RoleBased(
            "exporters", "Exporters", SecurityPermissions.Audit.Export, "auditor"));
        harness.Engine.RegisterPolicy(PolicyLibrary.Prohibition(
            "no-export", "No export", SecurityPermissions.Audit.Export, reason: "Exports are suspended."));

        var decision = harness.Engine.Authorize(
            User("u-root", "auditor"), SecurityPermissions.Audit.Export);

        Assert.True(decision.IsDenied);
        Assert.Equal(SecurityDecisionReason.DeniedByRule, decision.Reason);
        Assert.Equal("no-export:deny", decision.RuleKey);
    }

    [Fact]
    public void A_rule_grants_and_names_the_policy_that_did_it()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterPolicy(PolicyLibrary.RoleBased(
            "starters", "Starters", SecurityPermissions.Workflow.Start, "operator"));

        var decision = harness.Engine.Authorize(
            User("u-ada", "operator"), SecurityPermissions.Workflow.Start);

        Assert.True(decision.IsAllowed);
        Assert.Equal(SecurityDecisionReason.GrantedByRule, decision.Reason);
        Assert.Equal("starters", decision.PolicyKey);
    }

    [Fact]
    public void A_preview_decides_without_recording_anything()
    {
        var harness = Harness.Create(Now);

        var decision = harness.Engine.Preview(harness.Engine.Request()
            .For(User("u-ada"))
            .Requesting(SecurityPermissions.Workflow.Start)
            .Build());

        // Drawing a screen must not fill the trail with denials nobody attempted.
        Assert.True(decision.IsDenied);
        Assert.Empty(harness.Engine.Violations(Tenant));
        Assert.Empty(harness.Events.Events);
    }

    // ---- Policies and rules ------------------------------------------------------------------------------------

    [Fact]
    public void A_claim_based_policy_grants_to_whoever_presents_the_claim()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterPolicy(PolicyLibrary.ClaimBased(
            "shift-leads", "Shift leads", SecurityPermissions.HumanTask.Assign, "duty", "lead"));

        var lead = new SecurityPrincipal(
            "u-ada", Tenant, Authenticated, [SecurityClaim.Of("duty", "lead")]);
        var other = new SecurityPrincipal(
            "u-bob", Tenant, Authenticated, [SecurityClaim.Of("duty", "operator")]);

        Assert.True(harness.Engine.Authorize(lead, SecurityPermissions.HumanTask.Assign).IsAllowed);
        Assert.True(harness.Engine.Authorize(other, SecurityPermissions.HumanTask.Assign).IsDenied);
    }

    [Fact]
    public void A_resource_based_policy_grants_only_over_what_the_principal_owns()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterPolicy(PolicyLibrary.ResourceBased(
            "own-runs", "Own runs", SecurityPermissions.Workflow.Cancel));

        var mine = harness.Engine.Authorize(harness.Engine.Request()
            .For(User("u-ada"))
            .On(SecurityResource.Instance("workflow", "order-flow", "run-1", owner: "u-ada"))
            .To(SecurityAction.Of("cancel"))
            .Build());

        var theirs = harness.Engine.Authorize(harness.Engine.Request()
            .For(User("u-ada"))
            .On(SecurityResource.Instance("workflow", "order-flow", "run-2", owner: "u-bob"))
            .To(SecurityAction.Of("cancel"))
            .Build());

        Assert.True(mine.IsAllowed);
        Assert.True(theirs.IsDenied);
        Assert.Equal(SecurityDecisionReason.ConstraintNotSatisfied, theirs.Reason);
    }

    [Fact]
    public void A_tenant_based_policy_exists_only_for_the_tenant_it_names()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterPolicy(PolicyLibrary.TenantBased(
            "acme-starters", "Acme starters", Tenant, SecurityPermissions.Workflow.Start, "operator"));

        var here = harness.Engine.Authorize(
            User("u-ada", "operator"), SecurityPermissions.Workflow.Start);
        var elsewhere = harness.Engine.Authorize(harness.Engine.Request()
            .For(new SecurityPrincipal(
                "u-ada", "globex", Authenticated, [SecurityClaim.Of(SecurityClaim.RoleType, "operator")]))
            .Requesting(SecurityPermissions.Workflow.Start)
            .Build());

        Assert.True(here.IsAllowed);
        Assert.True(elsewhere.IsDenied);
        Assert.Single(harness.Engine.Policies(Tenant));
        Assert.Empty(harness.Engine.Policies("globex"));
    }

    [Fact]
    public void A_blocked_grant_says_what_would_have_to_change()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterPolicy(PolicyLibrary.IpBased(
            "plant-only", "Plant network only", SecurityPermissions.Audit.Export, "10.4.0.0/16"));

        var decision = harness.Engine.Authorize(harness.Engine.Request()
            .For(User("u-ada"))
            .Requesting(SecurityPermissions.Audit.Export)
            .From("203.0.113.7")
            .Build());

        Assert.Equal(SecurityDecisionReason.ConstraintNotSatisfied, decision.Reason);
        Assert.Equal("plant-only:network", decision.FailedConstraint);
        Assert.Contains("10.4.0.0/16", decision.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void A_constraint_narrows_its_own_rule_not_the_permission()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterPolicy(PolicyLibrary.IpBased(
            "plant-only", "Plant network only", SecurityPermissions.Audit.Export, "10.4.0.0/16"));
        harness.Engine.Grant(Tenant, "u-auditor", SecurityPermissions.Audit.Export, "u-root");

        var held = harness.Engine.Authorize(harness.Engine.Request()
            .For(User("u-auditor"))
            .Requesting(SecurityPermissions.Audit.Export)
            .From("203.0.113.7")
            .Build());

        // The direct grant is not narrowed by a constraint written on some other rule. To bind everybody
        // regardless of what they hold, a deny is what does it — and a deny always wins.
        Assert.True(held.IsAllowed);

        harness.Engine.RegisterPolicy(PolicyLibrary.Prohibition(
            "never-offsite", "Never off-site", SecurityPermissions.Audit.Export));

        Assert.True(harness.Engine.Authorize(harness.Engine.Request()
            .For(User("u-auditor"))
            .Requesting(SecurityPermissions.Audit.Export)
            .From("203.0.113.7")
            .Build()).IsDenied);
    }

    // ---- Constraints -------------------------------------------------------------------------------------------

    [Fact]
    public void A_time_window_admits_the_shift_it_names_and_nothing_else()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterPolicy(PolicyLibrary.TimeBased(
            "day-shift",
            "Day shift",
            SecurityPermissions.HumanTask.Complete,
            TimeWindowConstraint.WorkingWeek(
                "day-shift:hours", new TimeOnly(08, 00), new TimeOnly(17, 00), TimeSpan.FromHours(3))));

        // 09:00Z is 12:00 at a UTC+3 site on a Wednesday — inside the window.
        Assert.True(Decide(harness, Now).IsAllowed);

        // 20:00Z is 23:00 there — outside it.
        Assert.True(Decide(harness, Now.AddHours(11)).IsDenied);

        // And Saturday is outside it at any hour.
        Assert.True(Decide(harness, Now.AddDays(3)).IsDenied);

        static SecurityDecision Decide(Harness harness, DateTimeOffset at) =>
            harness.Engine.Authorize(harness.Engine.Request()
                .For(User("u-ada"))
                .Requesting(SecurityPermissions.HumanTask.Complete)
                .At(at)
                .Build());
    }

    [Fact]
    public void A_night_shift_window_runs_through_midnight()
    {
        var window = new TimeWindowConstraint(
            "night", new TimeOnly(22, 00), new TimeOnly(06, 00), [], TimeSpan.Zero);

        // Written as explicit UTC instants: an implicit DateTime conversion here would pick up whatever
        // offset the machine running the test happens to be in, and the assertion would mean nothing.
        Assert.True(window.IsSatisfiedBy(Context(At(23, 00))));
        Assert.True(window.IsSatisfiedBy(Context(At(02, 00))));
        Assert.False(window.IsSatisfiedBy(Context(At(12, 00))));

        static DateTimeOffset At(int hour, int minute) =>
            new(2026, 07, 22, hour, minute, 00, TimeSpan.Zero);
    }

    [Fact]
    public void A_request_from_an_unknown_address_is_not_on_the_plant_network()
    {
        var constraint = new IpRangeConstraint("plant", "10.4.0.0/16");

        Assert.True(constraint.IsSatisfiedBy(Context(Now, address: "10.4.7.9")));
        Assert.False(constraint.IsSatisfiedBy(Context(Now, address: "10.5.7.9")));

        // Unknown is not evidence of being inside. Treating it as such would make the constraint bypassable
        // by anything that simply omits the header the address was read from.
        Assert.False(constraint.IsSatisfiedBy(Context(Now)));
        Assert.False(constraint.IsSatisfiedBy(Context(Now, address: "not-an-address")));
    }

    [Fact]
    public void An_address_of_a_different_family_is_answered_no_rather_than_guessed_at()
    {
        var constraint = new IpRangeConstraint("plant", "10.4.0.0/16");

        Assert.False(constraint.IsSatisfiedBy(Context(Now, address: "::1")));
    }

    [Fact]
    public void A_single_address_range_admits_exactly_that_address()
    {
        var constraint = new IpRangeConstraint("gateway", "192.168.1.1");

        Assert.True(constraint.IsSatisfiedBy(Context(Now, address: "192.168.1.1")));
        Assert.False(constraint.IsSatisfiedBy(Context(Now, address: "192.168.1.2")));
    }

    [Fact]
    public void A_malformed_range_is_refused_when_it_is_written_not_when_it_is_relied_on()
    {
        Assert.Throws<FormatException>(() => new IpRangeConstraint("bad", "10.4.0.0/48"));
        Assert.Throws<FormatException>(() => new IpRangeConstraint("bad", "not-a-network"));
    }

    [Fact]
    public void An_attribute_based_policy_compares_the_two_sides_of_the_request()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterPolicy(PolicyLibrary.AttributeBased(
            "own-site",
            "Own site only",
            SecurityPermissions.Approval.Decide,
            SecuritySubjectRequirement.ForRoles("supervisor"),
            new ClaimMatchesResourceConstraint("own-site:match", "site", "site")));

        var supervisor = new SecurityPrincipal(
            "u-ada",
            Tenant,
            Authenticated,
            [SecurityClaim.Of(SecurityClaim.RoleType, "supervisor"), SecurityClaim.Of("site", "izmir")]);

        Assert.True(Decide(harness, supervisor, "izmir").IsAllowed);
        Assert.True(Decide(harness, supervisor, "bursa").IsDenied);

        static SecurityDecision Decide(Harness harness, SecurityPrincipal principal, string site) =>
            harness.Engine.Authorize(harness.Engine.Request()
                .For(principal)
                .On(SecurityResource.OfType("approval").With("site", site))
                .To(SecurityAction.Of("decide"))
                .Build());
    }

    [Fact]
    public void Two_unknowns_are_never_a_match()
    {
        var constraint = new ClaimMatchesResourceConstraint("match", "site", "site");

        // A missing claim compared against a missing attribute would otherwise grant on the strength of
        // knowing nothing at all.
        Assert.False(constraint.IsSatisfiedBy(Context(Now)));
    }

    [Fact]
    public void An_attribute_constraint_reads_from_where_it_was_told_to()
    {
        var resource = new AttributeConstraint(
            "state", SecurityAttributeSource.Resource, "state", "draft");
        var environment = new AttributeConstraint(
            "channel", SecurityAttributeSource.Environment, "channel", "console");

        Assert.True(resource.IsSatisfiedBy(Context(Now, resourceAttribute: ("state", "draft"))));
        Assert.False(resource.IsSatisfiedBy(Context(Now, resourceAttribute: ("state", "released"))));
        Assert.True(environment.IsSatisfiedBy(Context(Now, environment: ("channel", "console"))));
        Assert.False(environment.IsSatisfiedBy(Context(Now)));
    }

    [Fact]
    public void A_scope_contains_what_it_covers_and_not_its_siblings()
    {
        var tenantWide = SecurityScope.ForTenant(Tenant);
        var oneSite = new SecurityScope(Tenant, "plant-1");

        Assert.True(tenantWide.Contains(oneSite));
        Assert.False(oneSite.Contains(tenantWide));
        Assert.False(oneSite.Contains(new SecurityScope(Tenant, "plant-2")));
        Assert.False(tenantWide.Contains(SecurityScope.ForTenant("globex")));
    }

    // ---- Sessions ----------------------------------------------------------------------------------------------

    [Fact]
    public void A_session_slides_while_it_is_used_and_still_dies_on_its_absolute_clock()
    {
        var harness = Harness.Create(Now, new SecurityEngineOptions
        {
            SessionIdleTimeout = TimeSpan.FromMinutes(30),
            SessionAbsoluteLifetime = TimeSpan.FromHours(2),
        });

        var session = harness.Engine.CreateSession(Tenant, "u-ada").Session;

        harness.Clock.Advance(TimeSpan.FromMinutes(20));
        Assert.NotNull(harness.Engine.RenewSession(session.Id));

        harness.Clock.Advance(TimeSpan.FromMinutes(20));
        Assert.NotNull(harness.Engine.RenewSession(session.Id));

        // Steadily used, so the idle clock never fired — and the absolute one still does.
        harness.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Null(harness.Engine.RenewSession(session.Id));
    }

    [Fact]
    public void A_session_left_alone_expires_on_its_idle_clock()
    {
        var harness = Harness.Create(Now, new SecurityEngineOptions
        {
            SessionIdleTimeout = TimeSpan.FromMinutes(30),
        });

        var session = harness.Engine.CreateSession(Tenant, "u-ada").Session;
        harness.Clock.Advance(TimeSpan.FromMinutes(31));

        Assert.Null(harness.Engine.RenewSession(session.Id));
        Assert.Equal(SessionEndReason.IdleTimeout, session.InactiveReason(harness.Clock.UtcNow));
    }

    [Fact]
    public void Renewal_cannot_resurrect_a_session_that_already_ended()
    {
        var harness = Harness.Create(Now, new SecurityEngineOptions
        {
            SessionIdleTimeout = TimeSpan.FromMinutes(10),
        });

        var session = harness.Engine.CreateSession(Tenant, "u-ada").Session;
        harness.Clock.Advance(TimeSpan.FromMinutes(11));

        Assert.False(session.Renew(harness.Clock.UtcNow, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void Reaching_the_concurrent_limit_displaces_the_oldest_rather_than_refusing_the_newest()
    {
        var harness = Harness.Create(Now, new SecurityEngineOptions { MaxConcurrentSessions = 2 });

        var first = harness.Engine.CreateSession(Tenant, "u-ada").Session;
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = harness.Engine.CreateSession(Tenant, "u-ada").Session;
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        var creation = harness.Engine.CreateSession(Tenant, "u-ada");

        // Refusing would let anybody lock a colleague out by filling their quota from a machine they control.
        Assert.Equal([first.Id], creation.Displaced.Select(session => session.Id));
        Assert.Equal(
            [second.Id, creation.Session.Id],
            harness.Engine.ActiveSessions(Tenant, "u-ada").Select(session => session.Id));
    }

    [Fact]
    public void Revoking_a_session_revokes_the_tokens_bound_to_it()
    {
        var harness = Harness.Create(Now);
        var session = harness.Engine.CreateSession(Tenant, "u-ada").Session;
        var token = harness.Engine.IssueToken("u-ada", Tenant, "factoryos", session.Id);

        Assert.True(harness.Engine.ValidateToken(token.Handle, Tenant).IsValid);
        Assert.True(harness.Engine.RevokeSession(session.Id));

        // A sign-out whose tokens still worked would be a sign-out that signed nobody out.
        Assert.False(harness.Engine.ValidateToken(token.Handle, Tenant).IsValid);
    }

    [Fact]
    public void Signing_out_everywhere_ends_every_open_session()
    {
        var harness = Harness.Create(Now);
        harness.Engine.CreateSession(Tenant, "u-ada");
        harness.Engine.CreateSession(Tenant, "u-ada");

        Assert.Equal(2, harness.Engine.RevokeAllSessions(Tenant, "u-ada"));
        Assert.Empty(harness.Engine.ActiveSessions(Tenant, "u-ada"));
    }

    [Fact]
    public void Retiring_expired_sessions_records_why_each_one_ended()
    {
        var harness = Harness.Create(Now, new SecurityEngineOptions
        {
            SessionIdleTimeout = TimeSpan.FromMinutes(5),
        });

        harness.Engine.CreateSession(Tenant, "u-ada");
        harness.Clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(1, harness.Engine.RetireExpiredSessions(Tenant));

        var expired = Assert.Single(harness.Events.Events.OfType<SessionExpired>());
        Assert.Equal(SessionEndReason.IdleTimeout, expired.Reason);
    }

    // ---- Tokens ------------------------------------------------------------------------------------------------

    [Fact]
    public void A_token_is_good_until_it_expires()
    {
        var harness = Harness.Create(Now, new SecurityEngineOptions { TokenLifetime = TimeSpan.FromHours(1) });
        var token = harness.Engine.IssueToken("u-ada", Tenant, "factoryos");

        Assert.True(harness.Engine.ValidateToken(token.Handle, Tenant).IsValid);

        harness.Clock.Advance(TimeSpan.FromHours(2));
        Assert.False(harness.Engine.ValidateToken(token.Handle, Tenant).IsValid);
    }

    [Fact]
    public void A_revoked_token_stops_working_immediately()
    {
        var harness = Harness.Create(Now);
        var token = harness.Engine.IssueToken("u-ada", Tenant, "factoryos");

        Assert.True(harness.Engine.RevokeToken(token.Handle, "The laptop was lost."));

        var result = harness.Engine.ValidateToken(token.Handle, Tenant);

        // The property a self-contained signed token cannot give you, and the one that actually matters.
        Assert.False(result.IsValid);
        Assert.Contains("The laptop was lost", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_token_minted_for_one_tenant_is_useless_in_another()
    {
        var harness = Harness.Create(Now);
        var token = harness.Engine.IssueToken("u-ada", Tenant, "factoryos");

        var result = harness.Engine.ValidateToken(token.Handle, "globex");

        Assert.False(result.IsValid);
        Assert.Equal(SecurityDecisionReason.TenantMismatch, result.Reason);
    }

    [Fact]
    public void A_token_nobody_issued_is_not_accepted_on_its_own_say_so()
    {
        var harness = Harness.Create(Now);

        Assert.False(harness.Engine.ValidateToken("made-up-handle", Tenant).IsValid);
        Assert.False(harness.Engine.ValidateToken(string.Empty).IsValid);
    }

    [Fact]
    public void Authenticating_with_a_token_produces_the_principal_it_stands_for()
    {
        var harness = Harness.Create(Now);
        harness.Engine.RegisterRole(SecurityRole.Of("operator", "Operator", "workflow.start"));

        var session = harness.Engine.CreateSession(Tenant, "u-ada").Session;
        var token = harness.Engine.IssueToken(
            "u-ada", Tenant, "factoryos", session.Id,
            [SecurityClaim.Of(SecurityClaim.RoleType, "operator")]);

        var principal = harness.Engine.Authenticate(token.Handle, Tenant)!;

        Assert.Equal("u-ada", principal.Subject);
        Assert.True(principal.IsAuthenticated);
        Assert.Equal(session.Id, principal.SessionId);
        Assert.True(harness.Engine.HasPermission(principal, "workflow.start"));
    }

    [Fact]
    public void Presenting_a_token_that_is_no_longer_good_is_recorded_as_a_violation()
    {
        var harness = Harness.Create(Now);
        var token = harness.Engine.IssueToken("u-ada", Tenant, "factoryos");
        harness.Engine.RevokeToken(token.Handle, "Rotated.");

        Assert.Null(harness.Engine.Authenticate(token.Handle, Tenant));

        var violation = Assert.Single(harness.Engine.Violations(Tenant));
        Assert.Equal(SecurityViolationKind.InvalidToken, violation.Kind);
        Assert.Single(harness.Events.Events.OfType<AuthenticationFailed>());
    }

    // ---- Violations, incidents and risk -------------------------------------------------------------------------

    [Fact]
    public void A_refusal_is_recorded_and_announced()
    {
        var harness = Harness.Create(Now);

        harness.Engine.Authorize(User("u-ada"), SecurityPermissions.Audit.Export);

        var violation = Assert.Single(harness.Engine.Violations(Tenant));
        Assert.Equal(SecurityViolationKind.AuthorizationDenied, violation.Kind);
        Assert.Equal("audit.export", violation.Permission);
        Assert.Single(harness.Events.Events.OfType<AuthorizationFailed>());
        Assert.Single(harness.Events.Events.OfType<SecurityViolationDetected>());
    }

    [Fact]
    public void A_run_of_refusals_raises_one_incident_at_the_threshold()
    {
        var harness = Harness.Create(Now, new SecurityEngineOptions { IncidentThreshold = 3 });

        for (var attempt = 0; attempt < 6; attempt++)
        {
            harness.Engine.Authorize(User("u-ada"), SecurityPermissions.Audit.Export);
        }

        // Raised on the crossing, not on every violation past it — otherwise the moment the pattern appeared
        // would be buried under the ones that followed.
        var incident = Assert.Single(harness.Engine.Incidents(Tenant));
        Assert.Equal(3, incident.Violations.Count);
        Assert.Single(harness.Events.Events.OfType<SecurityIncidentCreated>());
    }

    [Fact]
    public void One_attempt_to_reach_across_tenants_is_critical_on_its_own()
    {
        var harness = Harness.Create(Now);

        harness.Engine.Authorize(harness.Engine.Request()
            .For(User("u-ada"))
            .Requesting(SecurityPermissions.Workflow.Read)
            .In(SecurityScope.ForTenant("globex"))
            .Build());

        // One refused read is a misconfiguration; one attempt to read another factory's data is not, and
        // averaging them into the same counter would let the second hide behind the first.
        var violation = Assert.Single(harness.Engine.Violations("globex"));
        Assert.Equal(SecurityViolationKind.CrossTenantAccess, violation.Kind);
        Assert.Equal(SecurityRiskLevel.Critical, violation.Risk);
    }

    [Fact]
    public void Risk_rises_with_how_often_something_happened()
    {
        Assert.Equal(SecurityRiskLevel.Informational, SecurityRisk.From(
            SecurityViolationKind.AuthorizationDenied, 0).Level);
        Assert.Equal(SecurityRiskLevel.Low, SecurityRisk.From(
            SecurityViolationKind.AuthorizationDenied, 2).Level);
        Assert.Equal(SecurityRiskLevel.Medium, SecurityRisk.From(
            SecurityViolationKind.AuthorizationDenied, 6).Level);
        Assert.Equal(SecurityRiskLevel.High, SecurityRisk.From(
            SecurityViolationKind.AuthorizationDenied, 12).Level);
        Assert.Equal(SecurityRiskLevel.Critical, SecurityRisk.From(
            SecurityViolationKind.AuthorizationDenied, 25).Level);
    }

    [Fact]
    public void An_incident_can_be_investigated_and_closed_once()
    {
        var incident = SecurityIncident.Raise(
            Tenant, "u-ada", SecurityViolationKind.AuthorizationDenied,
            SecurityRisk.From(SecurityViolationKind.AuthorizationDenied, 5), Now, []);

        Assert.True(incident.Investigate());
        Assert.False(incident.Investigate());
        Assert.True(incident.Close(Now, "The role was wrong."));
        Assert.False(incident.Close(Now, "Again."));
        Assert.Equal(SecurityIncidentStatus.Resolved, incident.Status);
    }

    // ---- Correlation -------------------------------------------------------------------------------------------

    [Fact]
    public void A_decision_carries_the_correlation_of_the_request_that_asked_for_it()
    {
        var harness = Harness.Create(Now);
        var correlation = new SecurityCorrelation("op-7", "trace-9", "req-3");

        var decision = harness.Engine.Authorize(harness.Engine.Request()
            .For(User("u-ada"))
            .Requesting(SecurityPermissions.Workflow.Start)
            .CorrelatedBy(correlation)
            .Build());

        Assert.Equal("op-7", decision.Correlation?.CorrelationId);
        Assert.Equal("trace-9", decision.Correlation?.TraceId);
        Assert.Equal("req-3", decision.Correlation?.RequestId);
    }

    [Fact]
    public void Everything_a_refusal_produces_carries_the_same_correlation()
    {
        var harness = Harness.Create(Now);
        var correlation = new SecurityCorrelation("op-7", "trace-9", "req-3");

        harness.Engine.Authorize(harness.Engine.Request()
            .For(User("u-ada"))
            .Requesting(SecurityPermissions.Workflow.Start)
            .CorrelatedBy(correlation)
            .Build());

        // A record of a denial that cannot be joined to the request it refused is one nobody can act on.
        Assert.All(
            harness.Events.Events,
            published => Assert.Equal("op-7", published.Correlation.CorrelationId));
        Assert.Equal("trace-9", Assert.Single(harness.Engine.Violations(Tenant)).Correlation.TraceId);
    }

    [Fact]
    public void The_engine_reports_on_itself()
    {
        var harness = Harness.Create(Now);
        harness.Engine.Grant(Tenant, "u-ada", SecurityPermissions.Workflow.Start, "u-root");

        harness.Engine.Authorize(User("u-ada"), SecurityPermissions.Workflow.Start);
        harness.Engine.Authorize(User("u-ada"), SecurityPermissions.Audit.Export);
        harness.Engine.CreateSession(Tenant, "u-ada");

        var snapshot = harness.Engine.Snapshot();

        Assert.Equal(1, snapshot.AuthorizationsGranted);
        Assert.Equal(1, snapshot.AuthorizationsDenied);
        Assert.Equal(1, snapshot.SessionsCreated);
        Assert.Equal(1, snapshot.Violations);
    }

    [Fact]
    public void The_permission_catalogue_covers_every_area_the_platform_has()
    {
        // Every catalogued permission parses, and each of the eleven areas is represented.
        Assert.All(SecurityPermissions.Catalogue, permission => SecurityPermission.Parse(permission));
        Assert.Equal(
            11,
            SecurityPermissions.Catalogue
                .Select(permission => SecurityPermission.Parse(permission).Resource)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    private static SecurityIdentity Authenticated { get; } = new("password", Now);

    private static SecurityPrincipal User(string subject, params string[] roles) =>
        new(
            subject,
            Tenant,
            Authenticated,
            roles.Select(role => SecurityClaim.Of(SecurityClaim.RoleType, role)));

    private static SecurityContext Context(
        DateTimeOffset at,
        string? address = null,
        (string Name, string Value)? resourceAttribute = null,
        (string Name, string Value)? environment = null)
    {
        var resource = SecurityResource.OfType("workflow");
        if (resourceAttribute is { } attribute)
        {
            resource = resource.With(attribute.Name, attribute.Value);
        }

        return new SecurityContext(
            User("u-ada"),
            resource,
            SecurityAction.Read,
            SecurityScope.ForTenant(Tenant),
            at,
            networkAddress: address,
            environment: environment is { } fact
                ? new Dictionary<string, string> { [fact.Name] = fact.Value }
                : null);
    }

    private sealed record Harness(
        SecurityEngine Engine,
        ClaimResolver Claims,
        InMemorySecurityEventSink Events,
        MutableClock Clock)
    {
        internal static Harness Create(DateTimeOffset now, SecurityEngineOptions? options = null)
        {
            var clock = new MutableClock(now);
            var engineOptions = options ?? new SecurityEngineOptions();
            var repository = new InMemorySecurityRepository();
            var store = new InMemorySecurityStore();
            var sessions = new InMemorySessionRepository();
            var tokens = new InMemoryTokenRepository();
            var events = new InMemorySecurityEventSink();
            var dispatcher = new SecurityDispatcher([events]);
            var metrics = new SecurityMetrics();

            var roles = new RoleResolver(repository);
            var claims = new ClaimResolver(repository, roles);
            var permissions = new PermissionEvaluator(claims);
            var policies = new PolicyEvaluator(repository);
            var authorization = new AuthorizationEngine(permissions, policies);
            var sessionManager = new SessionManager(sessions, engineOptions, clock);
            var tokenValidator = new TokenValidator(tokens, sessionManager, engineOptions, clock);

            var runtime = new SecurityRuntime(
                authorization, sessionManager, tokenValidator, claims, repository, store, dispatcher, metrics,
                engineOptions, clock);

            var engine = new SecurityEngine(
                runtime, authorization, permissions, sessionManager, tokenValidator, repository, metrics, clock);

            return new Harness(engine, claims, events, clock);
        }
    }
}
