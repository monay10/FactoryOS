using FactoryOS.Domain.Abstractions;
using FactoryOS.IntegrationTests.Persistence;
using FactoryOS.Plugins.Workflow.Approvals.Configuration;
using FactoryOS.Plugins.Workflow.Approvals.Domain;
using FactoryOS.Plugins.Workflow.Approvals.Execution;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Nodes;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Execution;
using FactoryOS.Plugins.Workflow.SLA.Configuration;
using FactoryOS.Plugins.Workflow.SLA.Domain;
using FactoryOS.Plugins.Workflow.SLA.Events;
using FactoryOS.Plugins.Workflow.SLA.Execution;
using FactoryOS.Plugins.Workflow.SLA.Integration;
using FactoryOS.Plugins.Workflow.Tasks.Configuration;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Execution;
using Microsoft.Extensions.DependencyInjection;
using WorkflowContext = FactoryOS.Plugins.Workflow.Engine.Configuration.WorkflowContext;

namespace FactoryOS.IntegrationTests.Workflow;

/// <summary>
/// The SLA engine composed through <c>AddSlaEngine</c> against a real container, tracking the work of the
/// workflow, human task and approval engines. An SLA is attached to a target by the orchestration layer and
/// closed when that work finishes — the SLA engine never reaches into those engines, and none of them is
/// modified or aware that SLAs exist. The opt-in notification bridge is exercised separately.
/// </summary>
public sealed class SlaEngineIntegrationTests
{
    // 2026-07-20 is a Monday, inside the working window of the calendar these tests register.
    private static readonly DateTimeOffset MondayNine = new(2026, 07, 20, 09, 00, 00, TimeSpan.Zero);

    private static (ServiceProvider Provider, FixedClock Clock) Build(bool withNotifications = false)
    {
        var clock = new FixedClock(MondayNine);
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(clock);

        if (withNotifications)
        {
            services.AddSlaNotificationIntegration();
        }
        else
        {
            services.AddSlaEngine();
            services.AddApprovalEngine();
            services.AddHumanTaskEngine();
        }

        return (services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }), clock);
    }

    [Fact]
    public void The_container_composes_the_sla_engine_without_pulling_in_notifications()
    {
        var (provider, _) = Build();
        using var scope = provider;

        Assert.NotNull(provider.GetRequiredService<SlaEngine>());
        Assert.NotNull(provider.GetRequiredService<BusinessTimeCalculator>());
        Assert.NotNull(provider.GetRequiredService<CalendarEngine>());

        // The core engine ships exactly one event sink — the recorder. No notification bridge is wired in.
        var sinks = provider.GetServices<ISlaEventSink>().ToArray();
        Assert.Single(sinks);
        Assert.IsType<InMemorySlaEventSink>(sinks[0]);
        Assert.Empty(provider.GetServices<NotificationEngine>());
    }

    [Fact]
    public async Task A_workflow_activity_sla_breaches_while_the_activity_stays_pending()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var slas = provider.GetRequiredService<SlaEngine>();

        var definition = WorkflowDefinition.Create("inspect-wf", "Inspection")
            .AddNode(new StartNode("s"))
            .AddNode(new ActivityNode("inspect", "Inspect", "inspect"))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "inspect")
            .AddTransition("inspect", "e")
            .Build();
        var run = await workflow.StartAsync(definition, WorkflowContext.Default);
        Assert.True(workflow.GetInstance(run.InstanceId)!.PendingActivities.ContainsKey("inspect"));

        var target = SlaTarget.ForWorkflowActivity("inspect", run.InstanceId);
        var sla = await slas.StartAsync(
            SlaDefinition.Create("activity-sla", "Activity SLA")
                .For(SlaTargetKind.WorkflowActivity)
                .WithDeadline(TimeSpan.FromHours(4))
                .Build(),
            target,
            SlaContext.Default);

        clock.UtcNow = MondayNine.AddHours(5);
        Assert.Equal(1, (await slas.RunDueAsync()).Breaches);
        Assert.Equal(SlaStatus.Breached, slas.GetSla(sla.Id)!.Status);

        // The workflow itself is untouched by the breach — it is still waiting on its activity.
        Assert.Equal(WorkflowStatus.Running, workflow.GetInstance(run.InstanceId)!.Status);

        // The orchestration layer completes the activity and closes the SLA against its target.
        await workflow.CompleteActivityAsync(run.InstanceId, "inspect");
        var closed = slas.CompleteForTarget(target, "u-alice")!;

        Assert.Equal(WorkflowStatus.Completed, workflow.GetInstance(run.InstanceId)!.Status);
        Assert.Equal(SlaOutcome.Breached, closed.Outcome);
    }

    [Fact]
    public async Task A_human_task_sla_reminds_then_closes_as_met_when_the_task_completes()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var slas = provider.GetRequiredService<SlaEngine>();
        var events = provider.GetServices<ISlaEventSink>().OfType<InMemorySlaEventSink>().Single();

        var task = await tasks.CreateAsync(
            HumanTaskDefinition.Create("inspect", "Inspect", HumanTaskAssignment.ToUser("u-bob")).Build(),
            HumanTaskContext.Default);

        var target = SlaTarget.ForHumanTask("inspect", task.Id);
        var sla = await slas.StartAsync(
            SlaDefinition.Create("task-sla", "Task SLA")
                .For(SlaTargetKind.HumanTask)
                .WithDeadline(TimeSpan.FromHours(4))
                .AddReminder(TimeSpan.FromHours(1))
                .Build(),
            target,
            SlaContext.Default);

        clock.UtcNow = MondayNine.AddHours(3).AddMinutes(30);
        Assert.Equal(1, (await slas.RunDueAsync()).Reminders);
        Assert.Contains(events.Events, e => e is SlaReminderTriggered);

        await tasks.ApproveAsync(task.Id, "u-bob");
        var closed = slas.CompleteForTarget(target, "u-bob")!;

        Assert.Equal(HumanTaskStatus.Completed, tasks.GetTask(task.Id)!.Status);
        Assert.Equal(SlaStatus.Completed, closed.Status);
        Assert.Equal(SlaOutcome.Met, closed.Outcome);
        Assert.Equal(sla.Id, closed.Id);
    }

    [Fact]
    public async Task An_approval_sla_escalates_on_a_business_calendar()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var approvals = provider.GetRequiredService<ApprovalEngine>();
        var slas = provider.GetRequiredService<SlaEngine>();

        slas.RegisterCalendar(new BusinessCalendar("factory")
            .AddWeekdays(new TimeOnly(09, 00), new TimeOnly(17, 00)));

        var approval = await approvals.StartAsync(
            ApprovalDefinition.Create("spend", "Spend Approval")
                .AddSingle("mgr", ApprovalAssignment.User("u-carol"))
                .Build(),
            ApprovalContext.Default);

        var sla = await slas.StartAsync(
            SlaDefinition.Create("approval-sla", "Approval SLA")
                .For(SlaTargetKind.Approval)
                .Using(SlaPolicy.WorkingHours("factory"))
                .WithDeadline(TimeSpan.FromHours(6))
                .AddEscalation(TimeSpan.FromHours(2), "u-director")
                .Build(),
            SlaTarget.ForApproval("spend", approval.Id),
            SlaContext.Default);

        // Six working hours from Monday 09:00 is Monday 15:00; the escalation is two working hours later.
        Assert.Equal(new DateTimeOffset(2026, 07, 20, 15, 00, 00, TimeSpan.Zero), sla.DueOnUtc);

        clock.UtcNow = new DateTimeOffset(2026, 07, 20, 17, 30, 00, TimeSpan.Zero);
        var pass = await slas.RunDueAsync();

        Assert.Equal(1, pass.Breaches);
        Assert.Equal(1, pass.Escalations);
        Assert.Equal(1, slas.GetSla(sla.Id)!.EscalationLevel);

        // The approval engine is entirely unaffected by its SLA breaching.
        Assert.Equal(ApprovalStatus.InProgress, approvals.GetApproval(approval.Id)!.Status);
    }

    [Fact]
    public async Task An_sla_escalation_reaches_the_notification_engine_through_the_opt_in_bridge()
    {
        var (provider, clock) = Build(withNotifications: true);
        using var scope = provider;
        var slas = provider.GetRequiredService<SlaEngine>();
        var notifications = provider.GetRequiredService<NotificationEngine>();

        // Both the recorder and the notification bridge observe the same SLA stream.
        var sinks = provider.GetServices<ISlaEventSink>().ToArray();
        Assert.Equal(2, sinks.Length);
        Assert.Contains(sinks, sink => sink is SlaNotificationBridge);

        await slas.StartAsync(
            SlaDefinition.Create("repair-sla", "Repair SLA")
                .WithDeadline(TimeSpan.FromHours(2))
                .AddEscalation(TimeSpan.FromMinutes(30), "u-supervisor")
                .Build(),
            SlaTarget.ForHumanTask("repair", Guid.NewGuid()),
            SlaContext.Default);

        clock.UtcNow = MondayNine.AddHours(3);
        await slas.RunDueAsync();

        var queued = Assert.Single(notifications.ListForRecipient("u-supervisor"));
        Assert.Equal(NotificationCategory.Escalation, queued.Category);
        Assert.Equal(NotificationPriority.High, queued.Priority);

        await notifications.ProcessDueAsync();
        Assert.Equal(NotificationStatus.Delivered, notifications.GetNotification(queued.Id)!.Status);
    }

    [Fact]
    public async Task Sla_state_and_history_are_persisted_and_readable()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var slas = provider.GetRequiredService<SlaEngine>();

        var target = SlaTarget.ForFormSubmission("check-sheet", Guid.NewGuid());
        var sla = await slas.StartAsync(
            SlaDefinition.Create("form-sla", "Form SLA")
                .For(SlaTargetKind.FormSubmission)
                .WithDeadline(TimeSpan.FromHours(4))
                .Build(),
            target,
            new SlaContext("acme", "u-alice"));

        clock.UtcNow = MondayNine.AddHours(1);
        slas.Pause(sla.Id, new PauseReason("waiting-on-lab"), "u-alice");
        clock.UtcNow = MondayNine.AddHours(2);
        slas.Resume(sla.Id, new ResumeReason("lab-returned"), "u-alice");

        var stored = slas.GetSla(sla.Id)!;
        Assert.Equal("acme", stored.Tenant);
        Assert.Equal(SlaStatus.Active, stored.Status);
        Assert.Equal(target, stored.Target);

        // The hour spent paused was given back: the deadline moved from 13:00 to 14:00.
        Assert.Equal(MondayNine.AddHours(5), stored.DueOnUtc);

        var actions = slas.GetHistory(sla.Id).Select(entry => entry.Action).ToArray();
        Assert.Contains(SlaHistoryAction.Started, actions);
        Assert.Contains(SlaHistoryAction.Paused, actions);
        Assert.Contains(SlaHistoryAction.Resumed, actions);
    }
}
