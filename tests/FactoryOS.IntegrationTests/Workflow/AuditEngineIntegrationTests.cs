using FactoryOS.Domain.Abstractions;
using FactoryOS.IntegrationTests.Persistence;
using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Forms.Engine.Execution;
using FactoryOS.Plugins.Workflow.Approvals.Configuration;
using FactoryOS.Plugins.Workflow.Approvals.Domain;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Approvals.Execution;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Audit.Sources;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Nodes;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.Notifications.Execution;
using FactoryOS.Plugins.Workflow.SLA.Configuration;
using FactoryOS.Plugins.Workflow.SLA.Domain;
using FactoryOS.Plugins.Workflow.SLA.Events;
using FactoryOS.Plugins.Workflow.SLA.Execution;
using FactoryOS.Plugins.Workflow.Tasks.Configuration;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Events;
using FactoryOS.Plugins.Workflow.Tasks.Execution;
using Microsoft.Extensions.DependencyInjection;
using NotificationContext = FactoryOS.Plugins.Workflow.Notifications.Configuration.NotificationContext;
using WorkflowContext = FactoryOS.Plugins.Workflow.Engine.Configuration.WorkflowContext;

namespace FactoryOS.IntegrationTests.Workflow;

/// <summary>
/// The audit engine composed through <c>AddAuditEngine</c> against a real container, recording the events of
/// every engine above it: workflow, forms, human task, approval, notification and SLA. None of those engines is
/// modified — where a seam allows only one consumer, audit wraps the existing registration in a composite and
/// appends itself, so the notification engine keeps receiving exactly what it did before.
/// </summary>
public sealed class AuditEngineIntegrationTests
{
    private const string Tenant = "default";
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 09, 00, 00, TimeSpan.Zero);

    private static (ServiceProvider Provider, FixedClock Clock) Build()
    {
        var clock = new FixedClock(Now);
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(clock);
        services.AddAuditEngine();
        return (services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }), clock);
    }

    [Fact]
    public void The_container_wraps_every_single_consumer_seam_without_displacing_what_was_there()
    {
        var (provider, _) = Build();
        using var scope = provider;

        Assert.NotNull(provider.GetRequiredService<AuditEngine>());

        // The five single-consumer seams are now composites…
        Assert.IsType<CompositeWorkflowEventSink>(provider.GetRequiredService<IWorkflowEventSink>());
        Assert.IsType<CompositeFormEventSink>(provider.GetRequiredService<IFormEventSink>());
        Assert.IsType<CompositeHumanTaskEventSink>(provider.GetRequiredService<IHumanTaskEventSink>());
        Assert.IsType<CompositeApprovalEventSink>(provider.GetRequiredService<IApprovalEventSink>());
        Assert.IsType<CompositeNotificationEventSink>(provider.GetRequiredService<INotificationEventSink>());

        // …and the SLA seam, which already fanned out, simply gained one more consumer.
        Assert.Contains(provider.GetServices<ISlaEventSink>(), sink => sink is SlaAuditSubscriber);

        // Every engine still resolves and is untouched.
        Assert.NotNull(provider.GetRequiredService<WorkflowEngine>());
        Assert.NotNull(provider.GetRequiredService<NotificationEngine>());
        Assert.NotNull(provider.GetRequiredService<SlaEngine>());
    }

    [Fact]
    public async Task A_workflow_run_is_recorded()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();

        var definition = WorkflowDefinition.Create("audited-wf", "Audited Workflow")
            .AddNode(new StartNode("s"))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "e")
            .Build();
        var run = await workflow.StartAsync(definition, WorkflowContext.Default);
        Assert.Equal(WorkflowStatus.Completed, workflow.GetInstance(run.InstanceId)!.Status);

        var records = audit.Search(new AuditQuery { Tenant = Tenant, Category = AuditCategory.Workflow });

        Assert.NotEmpty(records);
        Assert.Contains(records, record => record.Action == AuditAction.Completed);
        // The workflow instance is the correlation key, so its whole trail pulls together.
        Assert.All(records, record => Assert.Equal(run.InstanceId.ToString(), record.Correlation.CorrelationId));
        Assert.True(audit.Verify(Tenant).IsValid);
    }

    [Fact]
    public async Task A_form_submission_is_recorded()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var forms = provider.GetRequiredService<FormEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();

        var form = FormDefinition.Create("audited-form", "Audited Form")
            .AddSection(new FormSection("s", "Reading",
            [
                new FormGroup("g", null,
                [
                    new FormField(new FieldDefinition("value", "Value", FieldType.Decimal)
                    {
                        Validation = new FieldValidation { Required = true },
                    }),
                ]),
            ]))
            .Build();
        var instance = await forms.OpenAsync(form, FormContext.Default);
        var submission = await forms.SubmitAsync(instance.Id, new Dictionary<string, object?> { ["value"] = 7m });
        Assert.True(submission!.IsAccepted);

        var records = audit.Search(new AuditQuery { Tenant = Tenant, Category = AuditCategory.Form });

        Assert.Contains(records, record => record.Action == AuditAction.Submitted);
        Assert.Contains(records, record => record.Target.Key == "audited-form");
    }

    [Fact]
    public async Task A_human_task_lifecycle_is_recorded()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();

        var task = await tasks.CreateAsync(
            HumanTaskDefinition.Create("audited-task", "Inspect", HumanTaskAssignment.ToUser("u-bob")).Build(),
            HumanTaskContext.Default);
        await tasks.ApproveAsync(task.Id, "u-bob");

        var records = audit.Search(new AuditQuery
        {
            Tenant = Tenant,
            Category = AuditCategory.HumanTask,
            CorrelationId = task.Id.ToString(),
        });

        Assert.Contains(records, record => record.Action == AuditAction.Created);
        Assert.Contains(records, record => record.Action == AuditAction.Assigned);
        Assert.Contains(records, record => record.Action == AuditAction.Completed && record.Actor.Id == "u-bob");
    }

    [Fact]
    public async Task An_approval_decision_is_recorded()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var approvals = provider.GetRequiredService<ApprovalEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();

        var approval = await approvals.StartAsync(
            ApprovalDefinition.Create("audited-approval", "Spend")
                .AddSingle("mgr", ApprovalAssignment.User("u-carol"))
                .Build(),
            ApprovalContext.Default);
        await approvals.ApproveAsync(approval.Id, "mgr", "u-carol");

        var records = audit.Search(new AuditQuery
        {
            Tenant = Tenant,
            Category = AuditCategory.Approval,
            CorrelationId = approval.Id.ToString(),
        });

        Assert.Contains(records, record => record.Action == AuditAction.Approved && record.Actor.Id == "u-carol");
        Assert.Contains(records, record => record.Action == AuditAction.Completed);
    }

    [Fact]
    public async Task Notifications_are_recorded_and_still_delivered()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var notifications = provider.GetRequiredService<NotificationEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();

        var produced = await notifications.NotifyAsync(
            new NotificationRequest
            {
                Category = NotificationCategory.Alert,
                Recipients = [NotificationAssignment.ToUser("u-dana")],
                Subject = "Pressure high",
                Body = "Line 3 pressure is high.",
            },
            new NotificationContext(Tenant));
        var notification = Assert.Single(produced);

        // The notification engine still works exactly as before — the composite added a consumer, it did not
        // replace one.
        Assert.Equal(NotificationStatus.Delivered, notifications.GetNotification(notification.Id)!.Status);

        var records = audit.Search(new AuditQuery { Tenant = Tenant, Category = AuditCategory.Notification });
        Assert.Contains(records, record => record.Action == AuditAction.Queued);
        Assert.Contains(records, record => record.Action == AuditAction.Delivered);
    }

    [Fact]
    public async Task Sla_events_are_recorded_alongside_the_existing_consumers()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var slas = provider.GetRequiredService<SlaEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();

        var target = SlaTarget.ForHumanTask("inspect", Guid.NewGuid());
        var sla = await slas.StartAsync(
            SlaDefinition.Create("audited-sla", "Audited SLA")
                .WithDeadline(TimeSpan.FromHours(2))
                .Build(),
            target,
            SlaContext.Default);

        clock.UtcNow = Now.AddHours(3);
        await slas.RunDueAsync();
        slas.Complete(sla.Id, "u-erin");

        var records = audit.Search(new AuditQuery { Tenant = Tenant, Category = AuditCategory.Sla });

        Assert.Contains(records, record => record.Action == AuditAction.Started);
        // A breached deadline is a warning, not a failure of the audit trail itself.
        Assert.Contains(records, record => record.Action == AuditAction.Expired
            && record.Severity == AuditSeverity.Warning);
        Assert.Contains(records, record => record.Action == AuditAction.Completed);
        // The SLA's trail is correlated to the work it tracks, not to itself.
        Assert.All(records, record =>
            Assert.Equal(target.Id!.Value.ToString(), record.Correlation.CorrelationId));
    }

    [Fact]
    public async Task The_whole_platforms_trail_forms_one_verifiable_chain()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();

        await workflow.StartAsync(
            WorkflowDefinition.Create("mixed-wf", "Mixed")
                .AddNode(new StartNode("s")).AddNode(new EndNode("e")).AddTransition("s", "e").Build(),
            WorkflowContext.Default);
        var task = await tasks.CreateAsync(
            HumanTaskDefinition.Create("mixed-task", "Check", HumanTaskAssignment.ToUser("u-bob")).Build(),
            HumanTaskContext.Default);
        await tasks.ApproveAsync(task.Id, "u-bob");
        audit.Record(AuditEntries.SignIn(Tenant, "u-alice", succeeded: true));

        var trail = audit.ListByTenant(Tenant);

        // Records from six different sources share one sequence and one chain, in the order they happened.
        Assert.True(trail.Count >= 4);
        Assert.Equal(Enumerable.Range(1, trail.Count).Select(index => (long)index), trail.Select(r => r.Sequence));
        Assert.True(audit.Verify(Tenant).IsValid);

        var export = audit.Export(new AuditQuery { Tenant = Tenant, Limit = 1000 }, AuditExportFormat.Json, "u-auditor");
        Assert.Contains(trail[0].Hash, export, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_trail_survives_archiving_and_restore()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();
        audit.RegisterArchive(new AuditArchivePolicy(TimeSpan.FromDays(30)));

        await tasks.CreateAsync(
            HumanTaskDefinition.Create("aging-task", "Check", HumanTaskAssignment.ToUser("u-bob")).Build(),
            HumanTaskContext.Default);
        var before = audit.ListByTenant(Tenant).Count;
        Assert.True(before > 0);

        clock.UtcNow = Now.AddDays(31);
        Assert.Equal(before, audit.ArchiveDue());

        Assert.Empty(audit.ListByTenant(Tenant));
        var archived = audit.Archived(Tenant);
        Assert.Equal(before, archived.Count);
        Assert.True(audit.Verify(Tenant, includeArchived: true).IsValid);

        Assert.Equal(before, audit.Restore(Tenant, archived.Select(record => record.Id)));
        Assert.Equal(before, audit.ListByTenant(Tenant).Count);
        Assert.True(audit.Verify(Tenant).IsValid);
    }
}
