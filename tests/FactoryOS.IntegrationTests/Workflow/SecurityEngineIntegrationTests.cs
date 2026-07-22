using FactoryOS.Domain.Abstractions;
using FactoryOS.IntegrationTests.Persistence;
using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Execution;
using FactoryOS.Plugins.Workflow.Approvals.Configuration;
using FactoryOS.Plugins.Workflow.Approvals.Domain;
using FactoryOS.Plugins.Workflow.Approvals.Execution;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Nodes;
using FactoryOS.Plugins.Workflow.Engine.Persistence;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Events;
using FactoryOS.Plugins.Workflow.Security.Execution;
using FactoryOS.Plugins.Workflow.Security.Integration;
using FactoryOS.Plugins.Workflow.Security.Persistence;
using FactoryOS.Plugins.Workflow.Security.Policies;
using FactoryOS.Plugins.Workflow.Tasks.Configuration;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Execution;
using Microsoft.Extensions.DependencyInjection;
using WorkflowContext = FactoryOS.Plugins.Workflow.Engine.Configuration.WorkflowContext;

namespace FactoryOS.IntegrationTests.Workflow;

/// <summary>
/// The security engine composed through <c>AddSecurityEngine</c> against a real container, guarding operations
/// on the workflow, forms, human task, approval and connector surfaces — and producing an audit trail and
/// metrics through its two opt-in bridges.
/// <para>
/// None of those engines is modified, and none of them references the security namespace. Each test guards the
/// operation the way the application layer is meant to: ask, then act. What that proves is the important part
/// — a refusal stops the work <b>before</b> the engine is touched, so the engine's own state is the evidence
/// that authorization happened.
/// </para>
/// </summary>
public sealed class SecurityEngineIntegrationTests
{
    private const string Tenant = "default";
    private static readonly DateTimeOffset Now = new(2026, 07, 22, 09, 00, 00, TimeSpan.Zero);

    private static (ServiceProvider Provider, FixedClock Clock) Build(bool withBridges = false)
    {
        var clock = new FixedClock(Now);
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(clock);

        if (withBridges)
        {
            services.AddSecurityAuditIntegration();
            services.AddSecurityMonitoringIntegration();
        }
        else
        {
            services.AddSecurityEngine();
        }

        return (services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }), clock);
    }

    [Fact]
    public void The_core_registration_pulls_in_no_other_engine()
    {
        var (provider, _) = Build();
        using var scope = provider;

        Assert.NotNull(provider.GetRequiredService<SecurityEngine>());

        // Security is a platform service, not a consumer of the engines it guards. Registering it alone must
        // not drag the workflow, audit or monitoring engines into a container that never asked for them.
        Assert.Null(provider.GetService<WorkflowEngine>());
        Assert.Null(provider.GetService<AuditEngine>());
        Assert.Null(provider.GetService<MonitoringEngine>());
    }

    [Fact]
    public void The_bridges_attach_without_displacing_each_other()
    {
        var (provider, _) = Build(withBridges: true);
        using var scope = provider;

        var sinks = provider.GetServices<ISecurityEventSink>().ToArray();

        Assert.Contains(sinks, sink => sink is SecurityAuditBridge);
        Assert.Contains(sinks, sink => sink is SecurityMonitoringBridge);
        Assert.Contains(sinks, sink => sink is InMemorySecurityEventSink);
    }

    // ---- Workflow authorization ---------------------------------------------------------------------------------

    [Fact]
    public async Task Starting_a_workflow_is_refused_before_the_runtime_is_touched()
    {
        var (provider, _) = BuildWithEngines();
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();
        var workflows = provider.GetRequiredService<WorkflowEngine>();

        security.RegisterRole(SecurityRole.Of(
            "operator", "Operator", SecurityPermissions.Workflow.Start));

        var definition = WorkflowDefinition.Create("guarded-wf", "Guarded Workflow")
            .AddNode(new StartNode("s"))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "e")
            .Build();

        var visitor = User("u-visitor");
        var decision = security.Authorize(visitor, SecurityPermissions.Workflow.Start);

        // No policy addresses workflow.start here — the permission travels on a role — so the denial says
        // "nothing grants this to anyone" rather than "you personally lack it". The two are different
        // problems with different fixes, which is exactly why the engine keeps them apart.
        Assert.True(decision.IsDenied);
        Assert.Equal(SecurityDecisionReason.NoMatchingRule, decision.Reason);

        // The refusal is what stops the work; the runtime is never asked, so it has nothing to show for it.
        var store = provider.GetRequiredService<IWorkflowStore>();
        Assert.Empty(store.ListByTenant(Tenant));

        var operatorPrincipal = User("u-ada", "operator");
        Assert.True(security.Authorize(operatorPrincipal, SecurityPermissions.Workflow.Start).IsAllowed);

        await workflows.StartAsync(definition, WorkflowContext.Default);
        Assert.Single(store.ListByTenant(Tenant));
    }

    [Fact]
    public void A_workflow_run_can_be_cancelled_by_whoever_started_it_and_nobody_else()
    {
        var (provider, _) = BuildWithEngines();
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();

        security.RegisterPolicy(PolicyLibrary.ResourceBased(
            "own-runs", "Own runs", SecurityPermissions.Workflow.Cancel));

        var run = SecurityResource.Instance("workflow", "guarded-wf", "run-1", owner: "u-ada");

        Assert.True(Decide(security, User("u-ada"), run).IsAllowed);
        Assert.True(Decide(security, User("u-bob"), run).IsDenied);

        static SecurityDecision Decide(
            SecurityEngine security, SecurityPrincipal principal, SecurityResource resource) =>
            security.Authorize(security.Request()
                .For(principal)
                .On(resource)
                .To(SecurityAction.Of("cancel"))
                .Build());
    }

    // ---- Forms authorization ------------------------------------------------------------------------------------

    [Fact]
    public async Task Submitting_a_form_is_guarded_and_the_forms_engine_stays_unaware()
    {
        var (provider, _) = BuildWithEngines();
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();
        var forms = provider.GetRequiredService<FormEngine>();

        security.Grant(Tenant, "u-ada", SecurityPermissions.Forms.Submit, "u-root");

        var form = FormDefinition.Create("guarded-form", "Guarded Form")
            .AddSection(new FormSection("s", "Reading",
            [
                new FormGroup("g", null,
                [
                    new FormField(new FieldDefinition("value", "Value", FieldType.Decimal)),
                ]),
            ]))
            .Build();

        var instance = await forms.OpenAsync(form, FormContext.Default);

        Assert.True(security.Authorize(User("u-bob"), SecurityPermissions.Forms.Submit).IsDenied);
        Assert.True(security.Authorize(User("u-ada"), SecurityPermissions.Forms.Submit).IsAllowed);

        var submission = await forms.SubmitAsync(
            instance.Id, new Dictionary<string, object?> { ["value"] = 7m });

        Assert.True(submission!.IsAccepted);
    }

    // ---- Human task authorization -------------------------------------------------------------------------------

    [Fact]
    public async Task Completing_a_task_is_guarded_by_the_role_that_carries_the_permission()
    {
        var (provider, _) = BuildWithEngines();
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();
        var tasks = provider.GetRequiredService<HumanTaskEngine>();

        security.RegisterRole(SecurityRole.Of("operator", "Operator", SecurityPermissions.HumanTask.Read));
        security.RegisterRole(
            SecurityRole.Of("supervisor", "Supervisor", SecurityPermissions.HumanTask.Complete) with
            {
                Includes = ["operator"],
            });

        var task = await tasks.CreateAsync(
            HumanTaskDefinition.Create("guarded-task", "Inspect", HumanTaskAssignment.ToUser("u-bob")).Build(),
            HumanTaskContext.Default);

        Assert.True(security.Authorize(
            User("u-ada", "operator"), SecurityPermissions.HumanTask.Complete).IsDenied);

        var supervisor = User("u-bob", "supervisor");
        Assert.True(security.Authorize(supervisor, SecurityPermissions.HumanTask.Complete).IsAllowed);

        // Inheritance is effective, not just declared.
        Assert.True(security.HasPermission(supervisor, SecurityPermissions.HumanTask.Read));

        await tasks.ApproveAsync(task.Id, "u-bob");
        Assert.Equal(HumanTaskStatus.Completed, tasks.GetTask(task.Id)!.Status);
    }

    // ---- Approval authorization ---------------------------------------------------------------------------------

    [Fact]
    public async Task Deciding_an_approval_is_guarded_by_the_site_the_approver_belongs_to()
    {
        var (provider, _) = BuildWithEngines();
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();
        var approvals = provider.GetRequiredService<ApprovalEngine>();

        security.RegisterPolicy(PolicyLibrary.AttributeBased(
            "own-site",
            "Own site only",
            SecurityPermissions.Approval.Decide,
            SecuritySubjectRequirement.ForRoles("supervisor"),
            new ClaimMatchesResourceConstraint("own-site:match", "site", "site")));

        var izmir = new SecurityPrincipal(
            "u-carol",
            Tenant,
            Authenticated,
            [SecurityClaim.Of(SecurityClaim.RoleType, "supervisor"), SecurityClaim.Of("site", "izmir")]);

        var approval = await approvals.StartAsync(
            ApprovalDefinition.Create("guarded-approval", "Spend")
                .AddSingle("mgr", ApprovalAssignment.User("u-carol"))
                .Build(),
            ApprovalContext.Default);

        Assert.True(Decide(security, izmir, "bursa").IsDenied);
        Assert.True(Decide(security, izmir, "izmir").IsAllowed);

        await approvals.ApproveAsync(approval.Id, "mgr", "u-carol");
        Assert.Equal(
            ApprovalResolution.Approved, approvals.GetApproval(approval.Id)!.Resolution);

        static SecurityDecision Decide(SecurityEngine security, SecurityPrincipal principal, string site) =>
            security.Authorize(security.Request()
                .For(principal)
                .On(SecurityResource.OfType("approval").With("site", site))
                .To(SecurityAction.Of("decide"))
                .Build());
    }

    // ---- Connector authorization --------------------------------------------------------------------------------

    [Fact]
    public void Invoking_a_connector_is_guarded_by_where_the_request_came_from()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();

        // Reaching an outside system from off the plant network is the case this exists for.
        security.RegisterPolicy(PolicyLibrary.IpBased(
            "plant-only", "Plant network only", SecurityPermissions.Connector.Execute, "10.4.0.0/16"));

        Assert.True(Decide(security, "10.4.9.20").IsAllowed);

        var offsite = Decide(security, "203.0.113.7");
        Assert.True(offsite.IsDenied);
        Assert.Equal(SecurityDecisionReason.ConstraintNotSatisfied, offsite.Reason);
        Assert.Equal("plant-only:network", offsite.FailedConstraint);

        static SecurityDecision Decide(SecurityEngine security, string address) =>
            security.Authorize(security.Request()
                .For(User("u-ada"))
                .Requesting(SecurityPermissions.Connector.Execute)
                .From(address)
                .Build());
    }

    // ---- Persistence -------------------------------------------------------------------------------------------

    [Fact]
    public void Policies_grants_sessions_and_tokens_reach_the_stores_the_container_registered()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();
        var repository = provider.GetRequiredService<ISecurityRepository>();
        var sessions = provider.GetRequiredService<ISessionRepository>();
        var tokens = provider.GetRequiredService<ITokenRepository>();
        var store = provider.GetRequiredService<ISecurityStore>();

        security.RegisterRole(SecurityRole.Of("operator", "Operator", SecurityPermissions.Workflow.Start));
        security.RegisterPolicy(PolicyLibrary.RoleBased(
            "starters", "Starters", SecurityPermissions.Workflow.Start, "operator"));
        security.Grant(Tenant, "u-ada", SecurityPermissions.Audit.Read, "u-root");

        var session = security.CreateSession(Tenant, "u-ada", "10.4.0.9").Session;
        var token = security.IssueToken("u-ada", Tenant, "factoryos", session.Id);
        security.Authorize(User("u-bob"), SecurityPermissions.Audit.Export);

        Assert.Single(repository.PoliciesFor(Tenant));
        Assert.Single(repository.Roles());
        Assert.Equal([SecurityPermissions.Audit.Read], repository.GrantsFor(Tenant, "u-ada"));
        Assert.Equal(session.Id, sessions.Find(session.Id)!.Id);
        Assert.Equal("10.4.0.9", sessions.Find(session.Id)!.NetworkAddress);
        Assert.Equal(session.Id, tokens.Find(token.Handle)!.SessionId);
        Assert.Single(store.Violations(Tenant));
    }

    [Fact]
    public void A_grant_in_one_tenant_never_leaks_into_another()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();
        var repository = provider.GetRequiredService<ISecurityRepository>();

        security.Grant(Tenant, "u-ada", SecurityPermissions.Audit.Read, "u-root");

        Assert.Single(repository.GrantsFor(Tenant, "u-ada"));
        Assert.Empty(repository.GrantsFor("globex", "u-ada"));
    }

    // ---- Session management ------------------------------------------------------------------------------------

    [Fact]
    public void A_session_carries_a_token_through_authentication_until_it_is_revoked()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();

        security.RegisterRole(SecurityRole.Of("operator", "Operator", SecurityPermissions.Workflow.Start));

        var session = security.CreateSession(Tenant, "u-ada", "10.4.0.9").Session;
        var token = security.IssueToken(
            "u-ada", Tenant, "factoryos", session.Id,
            [SecurityClaim.Of(SecurityClaim.RoleType, "operator")]);

        var principal = security.Authenticate(token.Handle, Tenant)!;
        Assert.True(security.Authorize(principal, SecurityPermissions.Workflow.Start).IsAllowed);

        clock.UtcNow = Now.AddMinutes(20);
        Assert.NotNull(security.Authenticate(token.Handle, Tenant));

        security.RevokeSession(session.Id);
        Assert.Null(security.Authenticate(token.Handle, Tenant));
    }

    [Fact]
    public void An_idle_session_expires_even_though_its_token_has_not()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();

        var session = security.CreateSession(Tenant, "u-ada").Session;
        var token = security.IssueToken("u-ada", Tenant, "factoryos", session.Id);

        // Default idle timeout is 30 minutes; the token is good for an hour.
        clock.UtcNow = Now.AddMinutes(31);

        Assert.Null(security.Authenticate(token.Handle, Tenant));
        Assert.Empty(security.ActiveSessions(Tenant, "u-ada"));
    }

    // ---- Audit and monitoring integration ------------------------------------------------------------------------

    [Fact]
    public void Security_decisions_land_in_the_audit_trail()
    {
        var (provider, _) = Build(withBridges: true);
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();

        security.Grant(Tenant, "u-ada", SecurityPermissions.Workflow.Start, "u-root");
        security.Authorize(User("u-ada"), SecurityPermissions.Workflow.Start);
        security.Authorize(User("u-bob"), SecurityPermissions.Audit.Export);

        var records = audit.Search(new AuditQuery { Tenant = Tenant });

        Assert.Contains(records, record => record.Action == AuditAction.AccessGranted);
        Assert.Contains(
            records,
            record => record.Action == AuditAction.AccessDenied && record.Result == AuditResult.Denied);

        // The grant itself is administrative, so it is recorded as a change with the precise event named.
        Assert.Contains(records, record => record.EventType == "PermissionGranted");

        // And the whole trail is still one verifiable chain.
        Assert.True(audit.Verify(Tenant).IsValid);
    }

    [Fact]
    public void A_cross_tenant_attempt_is_recorded_at_the_severity_it_deserves()
    {
        var (provider, _) = Build(withBridges: true);
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();

        security.Authorize(security.Request()
            .For(User("u-ada"))
            .Requesting(SecurityPermissions.Workflow.Read)
            .In(SecurityScope.ForTenant("globex"))
            .Build());

        var records = audit.Search(new AuditQuery { Tenant = "globex" });

        Assert.Contains(
            records,
            record => record.Action == AuditAction.AccessDenied && record.Severity == AuditSeverity.Critical);
    }

    [Fact]
    public void Security_decisions_are_measured_and_refusals_are_sliced_by_reason()
    {
        var (provider, _) = Build(withBridges: true);
        using var scope = provider;
        var security = provider.GetRequiredService<SecurityEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

        security.Grant(Tenant, "u-ada", SecurityPermissions.Workflow.Start, "u-root");
        security.Authorize(User("u-ada"), SecurityPermissions.Workflow.Start);
        security.Authorize(User("u-bob"), SecurityPermissions.Audit.Export);
        security.CreateSession(Tenant, "u-ada");

        var granted = monitoring.Search(new MetricQuery(Tenant)
        {
            MetricKey = SecurityMetricCollection.AuthorizationsGranted,
        });
        var denied = monitoring.Search(new MetricQuery(Tenant)
        {
            MetricKey = SecurityMetricCollection.AuthorizationsDenied,
        });
        var sessions = monitoring.Search(new MetricQuery(Tenant)
        {
            MetricKey = SecurityMetricCollection.SessionsCreated,
        });

        Assert.Equal(1, Assert.Single(granted).Value);
        Assert.Equal(1, Assert.Single(sessions).Value);

        // A hundred MissingPermission denials is somebody's role being wrong; one TenantMismatch is not.
        var refusal = Assert.Single(denied);
        Assert.Equal("audit.export", refusal.Instance.Dimension["permission"]);
        Assert.Equal(
            nameof(SecurityDecisionReason.NoMatchingRule), refusal.Instance.Dimension["reason"]);
    }

    [Fact]
    public void The_engines_below_are_untouched_by_being_guarded()
    {
        var (provider, _) = BuildWithEngines();
        using var scope = provider;

        // Every engine still resolves, and none of them knows the security engine exists.
        Assert.NotNull(provider.GetRequiredService<WorkflowEngine>());
        Assert.NotNull(provider.GetRequiredService<FormEngine>());
        Assert.NotNull(provider.GetRequiredService<HumanTaskEngine>());
        Assert.NotNull(provider.GetRequiredService<ApprovalEngine>());
        Assert.NotNull(provider.GetRequiredService<AuditEngine>());
        Assert.NotNull(provider.GetRequiredService<MonitoringEngine>());
        Assert.NotNull(provider.GetRequiredService<SecurityEngine>());
    }

    private static (ServiceProvider Provider, FixedClock Clock) BuildWithEngines()
    {
        var clock = new FixedClock(Now);
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(clock);
        services.AddMonitoringEngine();
        services.AddSecurityAuditIntegration();
        services.AddSecurityMonitoringIntegration();
        return (services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }), clock);
    }

    private static SecurityIdentity Authenticated { get; } = new("password", Now);

    private static SecurityPrincipal User(string subject, params string[] roles) =>
        new(
            subject,
            Tenant,
            Authenticated,
            roles.Select(role => SecurityClaim.Of(SecurityClaim.RoleType, role)));
}
