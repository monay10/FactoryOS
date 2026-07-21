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
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Nodes;
using FactoryOS.Plugins.Workflow.Notifications.Channels;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Execution;
using FactoryOS.Plugins.Workflow.Notifications.Integration;
using FactoryOS.Plugins.Workflow.Tasks.Configuration;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Events;
using FactoryOS.Plugins.Workflow.Tasks.Execution;
using Microsoft.Extensions.DependencyInjection;
using NotificationContext = FactoryOS.Plugins.Workflow.Notifications.Configuration.NotificationContext;
using WorkflowContext = FactoryOS.Plugins.Workflow.Engine.Configuration.WorkflowContext;

namespace FactoryOS.IntegrationTests.Workflow;

/// <summary>
/// The notification engine composed through <c>AddNotificationEngine</c> against a real container, driven by the
/// events of the workflow, human task, approval and forms engines: each engine's lifecycle events flow into the
/// notification engine through its integration subscribers (which stand in as those engines' event sinks),
/// producing queued notifications that the queue processor delivers over the channels. None of the source
/// engines is modified or even aware that notifications exist.
/// </summary>
public sealed class NotificationEngineIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 14, 00, 00, TimeSpan.Zero);

    private static (ServiceProvider Provider, FixedClock Clock) Build()
    {
        var clock = new FixedClock(Now);
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(clock);
        services.AddNotificationEngine();
        return (services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }), clock);
    }

    [Fact]
    public void The_container_composes_the_notification_engine_and_wires_it_to_the_source_engines()
    {
        var (provider, _) = Build();
        using var scope = provider;

        Assert.NotNull(provider.GetRequiredService<NotificationEngine>());
        Assert.NotNull(provider.GetRequiredService<GenericEventSubscriber>());

        // The subscribers stand in as the source engines' event sinks — that is the event-bus wiring.
        Assert.IsType<WorkflowNotificationSubscriber>(provider.GetRequiredService<IWorkflowEventSink>());
        Assert.IsType<HumanTaskNotificationSubscriber>(provider.GetRequiredService<IHumanTaskEventSink>());
        Assert.IsType<ApprovalNotificationSubscriber>(provider.GetRequiredService<IApprovalEventSink>());
        Assert.IsType<FormsNotificationSubscriber>(provider.GetRequiredService<IFormEventSink>());

        // The source engines are composed but untouched.
        Assert.NotNull(provider.GetRequiredService<WorkflowEngine>());
        Assert.NotNull(provider.GetRequiredService<HumanTaskEngine>());
        Assert.NotNull(provider.GetRequiredService<ApprovalEngine>());
        Assert.NotNull(provider.GetRequiredService<FormEngine>());
    }

    [Fact]
    public async Task A_completed_workflow_produces_a_notification_for_its_subscribers()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var notifications = provider.GetRequiredService<NotificationEngine>();
        var outbox = provider.GetRequiredService<INotificationOutbox>();
        notifications.Subscribe(new NotificationSubscription("u-ops", NotificationCategory.Workflow));

        var definition = WorkflowDefinition.Create("wf-notify", "Notify Workflow")
            .AddNode(new StartNode("s"))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "e")
            .Build();
        var run = await workflow.StartAsync(definition, WorkflowContext.Default);
        Assert.Equal(WorkflowStatus.Completed, workflow.GetInstance(run.InstanceId)!.Status);

        var queued = Assert.Single(notifications.ListForRecipient("u-ops"));
        Assert.Equal(NotificationCategory.Workflow, queued.Category);

        var pass = await notifications.ProcessDueAsync();

        Assert.Equal(1, pass.Delivered);
        Assert.Equal(NotificationStatus.Delivered, notifications.GetNotification(queued.Id)!.Status);
        Assert.Single(outbox.ForChannel(NotificationChannel.InApp));
    }

    [Fact]
    public async Task A_created_human_task_notifies_its_assignee()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var notifications = provider.GetRequiredService<NotificationEngine>();

        var task = await tasks.CreateAsync(
            HumanTaskDefinition.Create("inspect", "Inspect", HumanTaskAssignment.ToUser("u-bob")).Build(),
            HumanTaskContext.Default);

        Assert.Equal(HumanTaskStatus.Waiting, tasks.GetTask(task.Id)!.Status);
        var queued = Assert.Single(notifications.ListForRecipient("u-bob"));
        Assert.Equal(NotificationCategory.HumanTask, queued.Category);
        Assert.Equal(task.Id, queued.SourceId);

        await notifications.ProcessDueAsync();

        Assert.Equal(NotificationStatus.Delivered, notifications.GetNotification(queued.Id)!.Status);
    }

    [Fact]
    public async Task A_started_approval_notifies_its_approver()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var approvals = provider.GetRequiredService<ApprovalEngine>();
        var notifications = provider.GetRequiredService<NotificationEngine>();

        var approval = await approvals.StartAsync(
            ApprovalDefinition.Create("spend", "Spend Approval")
                .AddSingle("mgr", ApprovalAssignment.User("u-carol"))
                .Build(),
            ApprovalContext.Default);

        var queued = Assert.Single(notifications.ListForRecipient("u-carol"));
        Assert.Equal(NotificationCategory.Approval, queued.Category);
        Assert.Equal(approval.Id, queued.SourceId);

        await notifications.ProcessDueAsync();

        Assert.Equal(NotificationStatus.Delivered, notifications.GetNotification(queued.Id)!.Status);
    }

    [Fact]
    public async Task A_submitted_form_produces_a_notification_for_its_subscribers()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var forms = provider.GetRequiredService<FormEngine>();
        var notifications = provider.GetRequiredService<NotificationEngine>();
        notifications.Subscribe(new NotificationSubscription("u-quality", NotificationCategory.Form));

        var form = FormDefinition.Create("check-sheet", "Check Sheet")
            .AddSection(new FormSection("s", "Check",
            [
                new FormGroup("g", null,
                [
                    new FormField(new FieldDefinition("reading", "Reading", FieldType.Decimal)
                    {
                        Validation = new FieldValidation { Required = true },
                    }),
                ]),
            ]))
            .Build();
        var instance = await forms.OpenAsync(form, FormContext.Default);
        var submission = await forms.SubmitAsync(instance.Id, new Dictionary<string, object?> { ["reading"] = 42m });
        Assert.True(submission!.IsAccepted);

        var queued = Assert.Single(notifications.ListForRecipient("u-quality"));
        Assert.Equal(NotificationCategory.Form, queued.Category);
        Assert.Equal("check-sheet", queued.SourceKey);
    }

    [Fact]
    public async Task The_queue_processor_delivers_a_scheduled_notification_only_once_it_is_due()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var notifications = provider.GetRequiredService<NotificationEngine>();

        var produced = notifications.Notify(
            new NotificationRequest
            {
                Category = NotificationCategory.General,
                Recipients = [NotificationAssignment.ToUser("u-dana")],
                DeliveryPolicy = NotificationDeliveryPolicy.Scheduled,
                ScheduledForUtc = Now.AddHours(2),
                Subject = "Planned outage",
                Body = "Line 3 stops at 22:00.",
            },
            new NotificationContext("acme"));
        var notification = Assert.Single(produced);

        Assert.Equal(0, (await notifications.ProcessDueAsync()).Delivered);
        Assert.Equal(NotificationStatus.Queued, notifications.GetNotification(notification.Id)!.Status);

        clock.UtcNow = Now.AddHours(3);

        Assert.Equal(1, (await notifications.ProcessDueAsync()).Delivered);
        Assert.Equal(NotificationStatus.Delivered, notifications.GetNotification(notification.Id)!.Status);
    }

    [Fact]
    public async Task Notification_state_and_history_are_persisted_and_readable()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var notifications = provider.GetRequiredService<NotificationEngine>();

        var produced = await notifications.NotifyAsync(
            new NotificationRequest
            {
                Category = NotificationCategory.Alert,
                Recipients = [NotificationAssignment.ToUser("u-erin")],
                Subject = "Alarm",
                Body = "Pressure high on {{asset}}.",
            },
            new NotificationContext("acme", values: new Dictionary<string, object?> { ["asset"] = "PUMP-7" }));
        var notification = Assert.Single(produced);

        Assert.Equal("Pressure high on PUMP-7.", notification.Body);
        Assert.Equal(NotificationStatus.Delivered, notification.Status);

        notifications.MarkRead(notification.Id);

        var stored = notifications.GetNotification(notification.Id)!;
        Assert.Equal(NotificationStatus.Read, stored.Status);
        var actions = notifications.GetHistory(notification.Id).Select(entry => entry.Action).ToArray();
        Assert.Contains(NotificationHistoryAction.Queued, actions);
        Assert.Contains(NotificationHistoryAction.Delivered, actions);
        Assert.Contains(NotificationHistoryAction.Read, actions);
    }
}
