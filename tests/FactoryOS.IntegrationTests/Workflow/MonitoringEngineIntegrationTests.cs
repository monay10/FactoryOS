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
using FactoryOS.Plugins.Workflow.Audit.Events;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Nodes;
using FactoryOS.Plugins.Workflow.Monitoring.Bridge;
using FactoryOS.Plugins.Workflow.Monitoring.Collections;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Events;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;
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
/// The monitoring engine composed through <c>AddMonitoringEngine</c> against a real container, measuring the
/// events of every engine above it: workflow, forms, human task, approval, notification, SLA and audit.
/// <para>
/// None of those engines is modified. Where a seam allows a single consumer, the bridge wraps the existing
/// registration and appends itself — so the notification and audit consumers that were already there keep
/// receiving exactly what they did before monitoring existed.
/// </para>
/// </summary>
public sealed class MonitoringEngineIntegrationTests
{
    private const string Tenant = "default";
    private static readonly DateTimeOffset Now = new(2026, 07, 22, 09, 00, 00, TimeSpan.Zero);

    private static (ServiceProvider Provider, FixedClock Clock) Build()
    {
        var clock = new FixedClock(Now);
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(clock);
        services.AddMonitoringEngine();
        return (services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }), clock);
    }

    [Fact]
    public void The_container_bridges_every_seam_without_displacing_what_was_there()
    {
        var (provider, _) = Build();
        using var scope = provider;

        Assert.NotNull(provider.GetRequiredService<MonitoringEngine>());

        // The five single-consumer seams are now bridges…
        Assert.IsType<WorkflowMetricsBridge>(provider.GetRequiredService<IWorkflowEventSink>());
        Assert.IsType<FormsMetricsBridge>(provider.GetRequiredService<IFormEventSink>());
        Assert.IsType<HumanTaskMetricsBridge>(provider.GetRequiredService<IHumanTaskEventSink>());
        Assert.IsType<ApprovalMetricsBridge>(provider.GetRequiredService<IApprovalEventSink>());
        Assert.IsType<NotificationMetricsBridge>(provider.GetRequiredService<INotificationEventSink>());

        // …and the two seams that already fanned out simply gained one more consumer.
        Assert.Contains(provider.GetServices<ISlaEventSink>(), sink => sink is SlaMetricsBridge);
        Assert.Contains(provider.GetServices<IAuditEventSink>(), sink => sink is AuditMetricsBridge);

        // Every engine below still resolves and is untouched.
        Assert.NotNull(provider.GetRequiredService<WorkflowEngine>());
        Assert.NotNull(provider.GetRequiredService<NotificationEngine>());
        Assert.NotNull(provider.GetRequiredService<SlaEngine>());
        Assert.NotNull(provider.GetRequiredService<AuditEngine>());
    }

    [Fact]
    public void The_whole_catalogue_and_every_health_check_are_registered_with_the_engine()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

        Assert.Equal(MetricCatalog.All.Count, monitoring.Definitions().Count);
        Assert.Equal(12, monitoring.HealthChecks().Count);
    }

    [Fact]
    public async Task A_workflow_run_is_measured_without_the_runtime_knowing()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

        var definition = WorkflowDefinition.Create("measured-wf", "Measured Workflow")
            .AddNode(new StartNode("s"))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "e")
            .Build();
        var run = await workflow.StartAsync(definition, WorkflowContext.Default);

        var started = Snapshot(monitoring, WorkflowMetricCollection.InstancesStarted, ByKey("measured-wf"));
        var completed = Snapshot(monitoring, WorkflowMetricCollection.InstancesCompleted, ByKey("measured-wf"));

        Assert.Equal(1, started.Value);

        // The completion event carries no definition key of its own; the bridge remembers it from the start,
        // so a completed run is filed beside the run that started it rather than under "unknown".
        Assert.Equal(1, completed.Value);
        Assert.Equal(run.InstanceId.ToString(), started.Correlation.CorrelationId);

        // And the run was timed, even though no engine measured anything.
        Assert.Equal(
            1, Snapshot(monitoring, WorkflowMetricCollection.InstanceDuration, ByKey("measured-wf")).Count);
    }

    [Fact]
    public async Task A_form_submission_is_measured_along_with_how_long_it_took_to_fill()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var forms = provider.GetRequiredService<FormEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

        var form = FormDefinition.Create("measured-form", "Measured Form")
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
        clock.UtcNow = Now.AddMinutes(2);
        var submission = await forms.SubmitAsync(instance.Id, new Dictionary<string, object?> { ["value"] = 7m });
        Assert.True(submission!.IsAccepted);

        Assert.Equal(
            1, Snapshot(monitoring, FormsMetricCollection.InstancesSubmitted, ByKey("measured-form")).Value);
        Assert.Equal(
            TimeSpan.FromMinutes(2).TotalMilliseconds,
            Snapshot(
                monitoring, FormsMetricCollection.FillDuration, ByKey("measured-form"),
                MetricAggregation.Average).Value);
    }

    [Fact]
    public async Task A_human_task_is_measured_and_its_notifications_still_go_out()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var notifications = provider.GetRequiredService<NotificationEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

        var task = await tasks.CreateAsync(
            HumanTaskDefinition.Create("measured-task", "Inspect", HumanTaskAssignment.ToUser("u-bob")).Build(),
            HumanTaskContext.Default);
        clock.UtcNow = Now.AddMinutes(4);
        await tasks.ApproveAsync(task.Id, "u-bob");

        Assert.Equal(1, Snapshot(monitoring, HumanTaskMetricCollection.Created, ByKey("measured-task")).Value);
        Assert.Equal(1, Snapshot(monitoring, HumanTaskMetricCollection.Completed, ByKey("measured-task")).Value);
        Assert.Equal(
            TimeSpan.FromMinutes(4).TotalMilliseconds,
            Snapshot(
                monitoring, HumanTaskMetricCollection.Duration, ByKey("measured-task"),
                MetricAggregation.Average).Value);

        // The notification engine was on this seam first and still receives everything it did before.
        Assert.NotEmpty(
            monitoring.Search(new MetricQuery(Tenant) { Category = MetricCategory.Notification }));
        Assert.NotNull(notifications);
    }

    [Fact]
    public async Task An_approval_is_measured_under_the_resolution_it_ended_with()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var approvals = provider.GetRequiredService<ApprovalEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

        var approval = await approvals.StartAsync(
            ApprovalDefinition.Create("measured-approval", "Spend")
                .AddSingle("mgr", ApprovalAssignment.User("u-carol"))
                .Build(),
            ApprovalContext.Default);
        clock.UtcNow = Now.AddMinutes(6);
        await approvals.ApproveAsync(approval.Id, "mgr", "u-carol");

        // How an approval ended is a slice of "approvals resolved", not a counter of its own.
        var resolved = Snapshot(
            monitoring,
            ApprovalMetricCollection.Completed,
            MetricDimension.Of(
                MetricLabel.Of("key", "measured-approval"), MetricLabel.Of("outcome", "Approved")));

        Assert.Equal(1, resolved.Value);
        Assert.Equal(1, Snapshot(monitoring, ApprovalMetricCollection.Approved, ByKey("measured-approval")).Value);
    }

    [Fact]
    public async Task Notification_delivery_is_measured_per_channel_and_still_delivered()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var notifications = provider.GetRequiredService<NotificationEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

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

        // Measuring the notification engine did not stop it delivering.
        Assert.Equal(NotificationStatus.Delivered, notifications.GetNotification(notification.Id)!.Status);

        var delivered = monitoring.Search(new MetricQuery(Tenant)
        {
            MetricKey = NotificationMetricCollection.Delivered,
        });

        var series = Assert.Single(delivered);
        Assert.Equal(notification.Channel.ToString(), series.Instance.Dimension["channel"]);
        Assert.Equal(1, series.Value);
    }

    [Fact]
    public async Task An_SLA_breach_and_a_timeout_are_measured_as_the_different_things_they_are()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var slas = provider.GetRequiredService<SlaEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

        var target = SlaTarget.ForHumanTask("inspect", Guid.NewGuid());
        await slas.StartAsync(
            SlaDefinition.Create("measured-sla", "Measured SLA")
                .WithDeadline(TimeSpan.FromHours(1))
                .WithTimeout(TimeSpan.FromHours(2))
                .Build(),
            target,
            SlaContext.Default);

        clock.UtcNow = Now.AddHours(3);
        await slas.RunDueAsync();

        // The SLA engine keeps a missed deadline and a hard timeout apart; so does the metric catalogue.
        Assert.Equal(1, Snapshot(monitoring, SlaMetricCollection.Breached, ByKey("measured-sla")).Value);
        Assert.Equal(1, Snapshot(monitoring, SlaMetricCollection.TimedOut, ByKey("measured-sla")).Value);
        Assert.Equal(1, Snapshot(monitoring, SlaMetricCollection.Started, ByKey("measured-sla")).Value);
    }

    [Fact]
    public async Task Audit_activity_is_measured_and_the_trail_is_still_written()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var audit = provider.GetRequiredService<AuditEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

        await tasks.CreateAsync(
            HumanTaskDefinition.Create("audited-task", "Inspect", HumanTaskAssignment.ToUser("u-bob")).Build(),
            HumanTaskContext.Default);

        Assert.True(Snapshot(monitoring, AuditMetricCollection.Recorded, MetricDimension.None).Value > 0);

        // Audit was already on the fan-out seam. Measuring it did not stop it recording, and the hash chain
        // it maintains is still intact.
        Assert.NotEmpty(audit.ListByTenant(Tenant));
        Assert.True(audit.Verify(Tenant).IsValid);
    }

    [Fact]
    public void Measurements_reach_the_stores_the_container_registered_with_their_correlation_intact()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var monitoring = provider.GetRequiredService<MonitoringEngine>();
        var metrics = provider.GetRequiredService<IMetricStore>();
        var health = provider.GetRequiredService<IHealthStore>();

        monitoring.Count(
            Tenant, ApiMetricCollection.Requests, ByKey("orders"),
            new MetricCorrelation("op-1", "trace-1", "req-1"));

        var series = Assert.Single(metrics.ListByMetric(Tenant, ApiMetricCollection.Requests));
        var value = Assert.Single(series.Values());

        Assert.Equal("op-1", value.Correlation.CorrelationId);
        Assert.Equal("trace-1", value.Correlation.TraceId);
        Assert.Equal("req-1", value.Correlation.RequestId);

        // Nothing has been probed yet, so the health store is genuinely empty rather than optimistically full.
        Assert.Empty(health.LatestAll(Tenant));
    }

    [Fact]
    public async Task Thresholds_and_alerts_run_over_what_the_engines_actually_did()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();
        var events = provider.GetServices<IMonitoringEventSink>().OfType<InMemoryMonitoringEventSink>().Single();

        monitoring.RegisterThreshold(new MetricThreshold(
            "task-volume", HumanTaskMetricCollection.Created, MetricComparison.GreaterThan, 1)
        {
            Window = TimeSpan.FromMinutes(30),
        });
        monitoring.RegisterAlertRule(new MetricAlertRule("too-many-tasks", "task-volume"));

        for (var index = 0; index < 3; index++)
        {
            await tasks.CreateAsync(
                HumanTaskDefinition.Create("busy-task", "Check", HumanTaskAssignment.ToUser("u-bob")).Build(),
                HumanTaskContext.Default);
        }

        monitoring.Evaluate(Tenant);

        var alert = Assert.Single(monitoring.OpenAlerts(Tenant));
        Assert.Equal("too-many-tasks", alert.RuleKey);
        Assert.Equal(3, alert.Value);
        Assert.Single(events.Events.OfType<AlertTriggered>());
        Assert.Single(events.Events.OfType<ThresholdExceeded>());
    }

    [Fact]
    public async Task The_health_report_covers_every_component_and_records_what_it_found()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();
        var health = provider.GetRequiredService<IHealthStore>();
        var events = provider.GetServices<IMonitoringEventSink>().OfType<InMemoryMonitoringEventSink>().Single();

        var task = await tasks.CreateAsync(
            HumanTaskDefinition.Create("healthy-task", "Check", HumanTaskAssignment.ToUser("u-bob")).Build(),
            HumanTaskContext.Default);
        await tasks.ApproveAsync(task.Id, "u-bob");

        var report = await monitoring.CheckHealthAsync(Tenant);

        Assert.Equal(12, report.Results.Count);
        Assert.Equal(12, health.LatestAll(Tenant).Count);
        Assert.Equal(12, events.Events.OfType<HealthCheckCompleted>().Count());

        // The human task engine completed work and expired none of it, so it is the one with a verdict.
        var humanTasks = Assert.Single(report.Results, result => result.Key == "human-task-engine");
        Assert.Equal(HealthStatus.Healthy, humanTasks.Status);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task A_platform_that_has_done_nothing_reports_unknown_rather_than_healthy()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

        var report = await monitoring.CheckHealthAsync(Tenant);

        // Silence is not health. A report that rounded this up to "healthy" would say "fine" during an outage.
        Assert.Equal(HealthStatus.Unknown, report.Status);
        Assert.All(report.Results, result => Assert.Equal(HealthStatus.Unknown, result.Status));
    }

    [Fact]
    public async Task Retention_prunes_what_the_engines_measured()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();
        monitoring.RegisterRetention(new MetricRetentionPolicy(
            TimeSpan.FromMinutes(30), MetricRetentionAction.Delete));

        await tasks.CreateAsync(
            HumanTaskDefinition.Create("aging-task", "Check", HumanTaskAssignment.ToUser("u-bob")).Build(),
            HumanTaskContext.Default);

        clock.UtcNow = Now.AddHours(2);
        var summary = monitoring.RunRetention(Tenant);

        Assert.True(summary.Removed > 0);
        Assert.All(
            provider.GetRequiredService<IMetricStore>().ListByTenant(Tenant),
            series => Assert.Equal(0, series.Count));
    }

    private static MetricSnapshot Snapshot(
        MonitoringEngine monitoring,
        string metricKey,
        MetricDimension dimension,
        MetricAggregation aggregation = MetricAggregation.Sum) =>
        monitoring.Snapshot(
            MetricInstance.Of(Tenant, metricKey, dimension), aggregation, TimeSpan.FromDays(1));

    private static MetricDimension ByKey(string value) => MetricDimension.Of(MetricLabel.Of("key", value));
}
