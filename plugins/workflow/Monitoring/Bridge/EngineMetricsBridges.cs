using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Audit.Events;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Monitoring.Collections;
using FactoryOS.Plugins.Workflow.Monitoring.Diagnostics;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.SLA.Events;
using FactoryOS.Plugins.Workflow.Tasks.Events;

namespace FactoryOS.Plugins.Workflow.Monitoring.Bridge;

/// <summary>
/// Measures the workflow runtime from its published events, and forwards them on unchanged.
/// <para>
/// The runtime's seam takes a single consumer, so this bridge wraps whatever was registered before it rather
/// than replacing it. Forwarding happens <b>first</b>: notifications and audit see the event on exactly the
/// same path they always did, and monitoring is appended behind them.
/// </para>
/// </summary>
public sealed class WorkflowMetricsBridge : MetricsBridge<WorkflowEvent>, IWorkflowEventSink
{
    private readonly IWorkflowEventSink? _inner;

    /// <summary>Initializes a new instance of the <see cref="WorkflowMetricsBridge"/> class.</summary>
    /// <param name="monitoring">The monitoring engine.</param>
    /// <param name="diagnostics">The engine's own counters.</param>
    /// <param name="inner">The consumer already on the seam, if any.</param>
    public WorkflowMetricsBridge(
        MonitoringEngine monitoring, MonitoringMetrics diagnostics, IWorkflowEventSink? inner = null)
        : base(monitoring, diagnostics) => _inner = inner;

    /// <inheritdoc />
    public void Publish(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);
        _inner?.Publish(workflowEvent);
        MeasureSafely(workflowEvent);
    }

    /// <inheritdoc />
    protected override void Measure(WorkflowEvent workflowEvent)
    {
        var tenant = workflowEvent.Tenant;
        var instanceId = workflowEvent.InstanceId.ToString();
        var correlation = MetricCorrelation.For(instanceId);
        var at = workflowEvent.OccurredOnUtc;

        switch (workflowEvent)
        {
            case WorkflowStarted started:
                Durations.Start(instanceId, at, started.DefinitionKey);
                Monitoring.Count(
                    tenant, WorkflowMetricCollection.InstancesStarted, ByKey(started.DefinitionKey), correlation, at);
                break;

            case WorkflowCompleted:
                Finish(tenant, WorkflowMetricCollection.InstancesCompleted, instanceId, correlation, at);
                break;

            case WorkflowFailed:
                Finish(tenant, WorkflowMetricCollection.InstancesFailed, instanceId, correlation, at);
                break;

            case WorkflowCancelled:
                Finish(tenant, WorkflowMetricCollection.InstancesCancelled, instanceId, correlation, at);
                break;

            case ActivityCompleted activity:
                Monitoring.Count(
                    tenant, WorkflowMetricCollection.ActivitiesCompleted, ByKey(activity.NodeId), correlation, at);
                break;

            case ActivityFailed activity:
                Monitoring.Count(
                    tenant, WorkflowMetricCollection.ActivitiesFailed, ByKey(activity.NodeId), correlation, at);
                break;

            default:
                // ActivityStarted and any event a later commit adds: counted when it ends, not when it begins.
                break;
        }
    }

    private void Finish(
        string tenant, string metricKey, string instanceId, MetricCorrelation correlation, DateTimeOffset at)
    {
        var run = Durations.Stop(instanceId, at);
        var dimension = ByKey(run?.Label ?? Configuration.MonitoringConstants.UnknownKey);
        Monitoring.Count(tenant, metricKey, dimension, correlation, at);

        if (run is { } finished)
        {
            Monitoring.Record(
                tenant, WorkflowMetricCollection.InstanceDuration, finished.Elapsed.TotalMilliseconds,
                dimension, correlation, at);
        }
    }
}

/// <summary>Measures the forms engine from its published events, and forwards them on unchanged.</summary>
public sealed class FormsMetricsBridge : MetricsBridge<FormEvent>, IFormEventSink
{
    private readonly IFormEventSink? _inner;

    /// <summary>Initializes a new instance of the <see cref="FormsMetricsBridge"/> class.</summary>
    /// <param name="monitoring">The monitoring engine.</param>
    /// <param name="diagnostics">The engine's own counters.</param>
    /// <param name="inner">The consumer already on the seam, if any.</param>
    public FormsMetricsBridge(
        MonitoringEngine monitoring, MonitoringMetrics diagnostics, IFormEventSink? inner = null)
        : base(monitoring, diagnostics) => _inner = inner;

    /// <inheritdoc />
    public void Publish(FormEvent formEvent)
    {
        ArgumentNullException.ThrowIfNull(formEvent);
        _inner?.Publish(formEvent);
        MeasureSafely(formEvent);
    }

    /// <inheritdoc />
    protected override void Measure(FormEvent formEvent)
    {
        var tenant = formEvent.Tenant;
        var dimension = ByKey(formEvent.FormKey);
        var at = formEvent.OccurredOnUtc;
        var instanceId = formEvent is FormInstanceEvent instance ? instance.InstanceId.ToString() : null;
        var correlation = MetricCorrelation.For(instanceId ?? formEvent.FormKey);

        switch (formEvent)
        {
            case FormPublished:
                Monitoring.Count(
                    tenant, FormsMetricCollection.DefinitionsPublished, dimension, correlation, at);
                break;

            case FormOpened opened:
                Durations.Start(opened.InstanceId.ToString(), at, formEvent.FormKey);
                Monitoring.Count(tenant, FormsMetricCollection.InstancesOpened, dimension, correlation, at);
                break;

            case FormSubmitted submitted:
                Monitoring.Count(tenant, FormsMetricCollection.InstancesSubmitted, dimension, correlation, at);
                if (Durations.Stop(submitted.InstanceId.ToString(), at) is { } fill)
                {
                    Monitoring.Record(
                        tenant, FormsMetricCollection.FillDuration, fill.Elapsed.TotalMilliseconds,
                        dimension, correlation, at);
                }

                break;

            case FormApproved:
                Monitoring.Count(tenant, FormsMetricCollection.InstancesApproved, dimension, correlation, at);
                break;

            case FormRejected:
                Monitoring.Count(tenant, FormsMetricCollection.InstancesRejected, dimension, correlation, at);
                break;

            case FormCancelled:
                Monitoring.Count(tenant, FormsMetricCollection.InstancesCancelled, dimension, correlation, at);
                break;

            default:
                // FormCreated, FormUpdated and FormSaved describe authoring and drafts, not throughput.
                break;
        }
    }
}

/// <summary>Measures the human task engine from its published events, and forwards them on unchanged.</summary>
public sealed class HumanTaskMetricsBridge : MetricsBridge<HumanTaskEvent>, IHumanTaskEventSink
{
    private readonly IHumanTaskEventSink? _inner;

    /// <summary>Initializes a new instance of the <see cref="HumanTaskMetricsBridge"/> class.</summary>
    /// <param name="monitoring">The monitoring engine.</param>
    /// <param name="diagnostics">The engine's own counters.</param>
    /// <param name="inner">The consumer already on the seam, if any.</param>
    public HumanTaskMetricsBridge(
        MonitoringEngine monitoring, MonitoringMetrics diagnostics, IHumanTaskEventSink? inner = null)
        : base(monitoring, diagnostics) => _inner = inner;

    /// <inheritdoc />
    public void Publish(HumanTaskEvent taskEvent)
    {
        ArgumentNullException.ThrowIfNull(taskEvent);
        _inner?.Publish(taskEvent);
        MeasureSafely(taskEvent);
    }

    /// <inheritdoc />
    protected override void Measure(HumanTaskEvent taskEvent)
    {
        var tenant = taskEvent.Tenant;
        var dimension = ByKey(taskEvent.DefinitionKey);
        var taskId = taskEvent.TaskId.ToString();
        var correlation = MetricCorrelation.For(taskId);
        var at = taskEvent.OccurredOnUtc;

        switch (taskEvent)
        {
            case HumanTaskCreated:
                Durations.Start(taskId, at, taskEvent.DefinitionKey);
                Monitoring.Count(tenant, HumanTaskMetricCollection.Created, dimension, correlation, at);
                break;

            case HumanTaskAssigned:
                Monitoring.Count(tenant, HumanTaskMetricCollection.Assigned, dimension, correlation, at);
                break;

            case HumanTaskCompleted:
                Finish(tenant, HumanTaskMetricCollection.Completed, taskId, dimension, correlation, at);
                break;

            case HumanTaskRejected:
                Finish(tenant, HumanTaskMetricCollection.Rejected, taskId, dimension, correlation, at);
                break;

            case HumanTaskCancelled:
                Finish(tenant, HumanTaskMetricCollection.Cancelled, taskId, dimension, correlation, at);
                break;

            case HumanTaskExpired:
                Finish(tenant, HumanTaskMetricCollection.Expired, taskId, dimension, correlation, at);
                break;

            case HumanTaskEscalated:
                Monitoring.Count(tenant, HumanTaskMetricCollection.Escalated, dimension, correlation, at);
                break;

            default:
                // HumanTaskOpened and HumanTaskReassigned move a task along without ending it.
                break;
        }
    }

    private void Finish(
        string tenant,
        string metricKey,
        string taskId,
        MetricDimension dimension,
        MetricCorrelation correlation,
        DateTimeOffset at)
    {
        Monitoring.Count(tenant, metricKey, dimension, correlation, at);
        if (Durations.Stop(taskId, at) is { } run)
        {
            Monitoring.Record(
                tenant, HumanTaskMetricCollection.Duration, run.Elapsed.TotalMilliseconds,
                dimension, correlation, at);
        }
    }
}

/// <summary>Measures the approval engine from its published events, and forwards them on unchanged.</summary>
public sealed class ApprovalMetricsBridge : MetricsBridge<ApprovalEvent>, IApprovalEventSink
{
    private readonly IApprovalEventSink? _inner;

    /// <summary>Initializes a new instance of the <see cref="ApprovalMetricsBridge"/> class.</summary>
    /// <param name="monitoring">The monitoring engine.</param>
    /// <param name="diagnostics">The engine's own counters.</param>
    /// <param name="inner">The consumer already on the seam, if any.</param>
    public ApprovalMetricsBridge(
        MonitoringEngine monitoring, MonitoringMetrics diagnostics, IApprovalEventSink? inner = null)
        : base(monitoring, diagnostics) => _inner = inner;

    /// <inheritdoc />
    public void Publish(ApprovalEvent approvalEvent)
    {
        ArgumentNullException.ThrowIfNull(approvalEvent);
        _inner?.Publish(approvalEvent);
        MeasureSafely(approvalEvent);
    }

    /// <inheritdoc />
    protected override void Measure(ApprovalEvent approvalEvent)
    {
        var tenant = approvalEvent.Tenant;
        var dimension = ByKey(approvalEvent.DefinitionKey);
        var approvalId = approvalEvent.ApprovalId.ToString();
        var correlation = MetricCorrelation.For(approvalId);
        var at = approvalEvent.OccurredOnUtc;

        switch (approvalEvent)
        {
            case ApprovalCreated:
                Durations.Start(approvalId, at, approvalEvent.DefinitionKey);
                Monitoring.Count(tenant, ApprovalMetricCollection.Created, dimension, correlation, at);
                break;

            case ApprovalApproved:
                Monitoring.Count(tenant, ApprovalMetricCollection.Approved, dimension, correlation, at);
                break;

            case ApprovalRejected:
                Monitoring.Count(tenant, ApprovalMetricCollection.Rejected, dimension, correlation, at);
                break;

            case ApprovalCompleted completed:
                // The resolution is a label rather than a metric of its own: how an approval ended is a slice
                // of "approvals resolved", and splitting it into separate counters would make the total the
                // one number nobody could read.
                Monitoring.Count(
                    tenant,
                    ApprovalMetricCollection.Completed,
                    ByOutcome(approvalEvent.DefinitionKey, completed.Resolution.ToString()),
                    correlation,
                    at);

                if (Durations.Stop(approvalId, at) is { } run)
                {
                    Monitoring.Record(
                        tenant, ApprovalMetricCollection.Duration, run.Elapsed.TotalMilliseconds,
                        dimension, correlation, at);
                }

                break;

            case ApprovalExpired:
                Monitoring.Count(tenant, ApprovalMetricCollection.Expired, dimension, correlation, at);
                break;

            case ApprovalEscalated:
                Monitoring.Count(tenant, ApprovalMetricCollection.Escalated, dimension, correlation, at);
                break;

            default:
                // ApprovalStarted, ApprovalAssigned, ApprovalCancelled and ApprovalReminderSent move an
                // approval along; the counters above already cover how it began and how it ended.
                break;
        }
    }
}

/// <summary>Measures the notification engine from its published events, and forwards them on unchanged.</summary>
public sealed class NotificationMetricsBridge : MetricsBridge<NotificationEvent>, INotificationEventSink
{
    private readonly INotificationEventSink? _inner;

    /// <summary>Initializes a new instance of the <see cref="NotificationMetricsBridge"/> class.</summary>
    /// <param name="monitoring">The monitoring engine.</param>
    /// <param name="diagnostics">The engine's own counters.</param>
    /// <param name="inner">The consumer already on the seam, if any.</param>
    public NotificationMetricsBridge(
        MonitoringEngine monitoring, MonitoringMetrics diagnostics, INotificationEventSink? inner = null)
        : base(monitoring, diagnostics) => _inner = inner;

    /// <inheritdoc />
    public void Publish(NotificationEvent notificationEvent)
    {
        ArgumentNullException.ThrowIfNull(notificationEvent);
        _inner?.Publish(notificationEvent);
        MeasureSafely(notificationEvent);
    }

    /// <inheritdoc />
    protected override void Measure(NotificationEvent notificationEvent)
    {
        var tenant = notificationEvent.Tenant;
        var channel = notificationEvent.Channel.ToString();
        var dimension = MetricDimension.Of(
            MetricLabel.Of(Configuration.MonitoringConstants.ChannelLabel, channel));
        var notificationId = notificationEvent.NotificationId.ToString();
        var correlation = MetricCorrelation.For(notificationId);
        var at = notificationEvent.OccurredOnUtc;

        switch (notificationEvent)
        {
            case NotificationQueued:
                Durations.Start(notificationId, at, channel);
                Monitoring.Count(tenant, NotificationMetricCollection.Queued, dimension, correlation, at);
                break;

            case NotificationSent:
                Monitoring.Count(tenant, NotificationMetricCollection.Sent, dimension, correlation, at);
                break;

            case NotificationDelivered:
                Monitoring.Count(tenant, NotificationMetricCollection.Delivered, dimension, correlation, at);
                if (Durations.Stop(notificationId, at) is { } run)
                {
                    Monitoring.Record(
                        tenant, NotificationMetricCollection.DeliveryDuration, run.Elapsed.TotalMilliseconds,
                        dimension, correlation, at);
                }

                break;

            case NotificationFailed failed:
                Monitoring.Count(tenant, NotificationMetricCollection.Failed, dimension, correlation, at);
                if (failed.DeadLettered)
                {
                    // A dead letter is the end of the road, not another failure; counting it separately is
                    // what lets an alert distinguish "retrying" from "given up".
                    Monitoring.Count(
                        tenant, NotificationMetricCollection.DeadLettered, dimension, correlation, at);
                    Durations.Stop(notificationId, at);
                }

                break;

            case NotificationRetried:
                Monitoring.Count(tenant, NotificationMetricCollection.Retried, dimension, correlation, at);
                break;

            case NotificationSuppressed:
                Monitoring.Count(tenant, NotificationMetricCollection.Suppressed, dimension, correlation, at);
                break;

            case NotificationExpired:
                Monitoring.Count(tenant, NotificationMetricCollection.Expired, dimension, correlation, at);
                Durations.Stop(notificationId, at);
                break;

            default:
                // NotificationSending, NotificationRead and NotificationCancelled are steps, not outcomes.
                break;
        }
    }
}

/// <summary>
/// Measures the SLA engine from its published events. The SLA seam already fans out, so this bridge simply
/// registers alongside the consumers that are already on it rather than wrapping any of them.
/// </summary>
public sealed class SlaMetricsBridge : MetricsBridge<SlaEvent>, ISlaEventSink
{
    /// <summary>Initializes a new instance of the <see cref="SlaMetricsBridge"/> class.</summary>
    /// <param name="monitoring">The monitoring engine.</param>
    /// <param name="diagnostics">The engine's own counters.</param>
    public SlaMetricsBridge(MonitoringEngine monitoring, MonitoringMetrics diagnostics)
        : base(monitoring, diagnostics)
    {
    }

    /// <inheritdoc />
    public void Publish(SlaEvent slaEvent)
    {
        ArgumentNullException.ThrowIfNull(slaEvent);
        MeasureSafely(slaEvent);
    }

    /// <inheritdoc />
    protected override void Measure(SlaEvent slaEvent)
    {
        var tenant = slaEvent.Tenant;
        var dimension = ByKey(slaEvent.DefinitionKey);
        var slaId = slaEvent.SlaId.ToString();
        var correlation = new MetricCorrelation(slaEvent.Target.Id?.ToString() ?? slaId);
        var at = slaEvent.OccurredOnUtc;

        switch (slaEvent)
        {
            case SlaStarted:
                Durations.Start(slaId, at, slaEvent.DefinitionKey);
                Monitoring.Count(tenant, SlaMetricCollection.Started, dimension, correlation, at);
                break;

            case SlaPaused:
                Monitoring.Count(tenant, SlaMetricCollection.Paused, dimension, correlation, at);
                break;

            case SlaResumed:
                Monitoring.Count(tenant, SlaMetricCollection.Resumed, dimension, correlation, at);
                break;

            case SlaReminderTriggered:
                Monitoring.Count(tenant, SlaMetricCollection.Reminders, dimension, correlation, at);
                break;

            case SlaEscalated:
                Monitoring.Count(tenant, SlaMetricCollection.Escalations, dimension, correlation, at);
                break;

            case SlaExpired:
                // A missed deadline; the SLA keeps running, so it is not an ending.
                Monitoring.Count(tenant, SlaMetricCollection.Breached, dimension, correlation, at);
                break;

            case SlaTimedOut:
                // A hard timeout ends the SLA. Kept apart from a breach exactly as the SLA engine keeps them
                // apart: collapsing the two would erase the distinction that engine was built to make.
                Monitoring.Count(tenant, SlaMetricCollection.TimedOut, dimension, correlation, at);
                Finish(tenant, slaId, dimension, correlation, at);
                break;

            case SlaCompleted completed:
                Monitoring.Count(
                    tenant,
                    SlaMetricCollection.Completed,
                    ByOutcome(slaEvent.DefinitionKey, completed.Outcome.ToString()),
                    correlation,
                    at);
                Finish(tenant, slaId, dimension, correlation, at);
                break;

            default:
                // SlaCancelled closes the SLA without a verdict on the work it tracked.
                Durations.Stop(slaId, at);
                break;
        }
    }

    private void Finish(
        string tenant,
        string slaId,
        MetricDimension dimension,
        MetricCorrelation correlation,
        DateTimeOffset at)
    {
        if (Durations.Stop(slaId, at) is { } run)
        {
            Monitoring.Record(
                tenant, SlaMetricCollection.Duration, run.Elapsed.TotalMilliseconds, dimension, correlation, at);
        }
    }
}

/// <summary>
/// Measures the audit engine from its published events. Like the SLA seam, the audit seam already fans out, so
/// this bridge joins it without displacing the recorder that is already there.
/// </summary>
public sealed class AuditMetricsBridge : MetricsBridge<AuditEvent>, IAuditEventSink
{
    /// <summary>Initializes a new instance of the <see cref="AuditMetricsBridge"/> class.</summary>
    /// <param name="monitoring">The monitoring engine.</param>
    /// <param name="diagnostics">The engine's own counters.</param>
    public AuditMetricsBridge(MonitoringEngine monitoring, MonitoringMetrics diagnostics)
        : base(monitoring, diagnostics)
    {
    }

    /// <inheritdoc />
    public void Publish(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        MeasureSafely(auditEvent);
    }

    /// <inheritdoc />
    protected override void Measure(AuditEvent auditEvent)
    {
        var tenant = auditEvent.Tenant;
        var at = auditEvent.OccurredOnUtc;

        switch (auditEvent)
        {
            case AuditRecorded recorded:
                Monitoring.Count(
                    tenant,
                    AuditMetricCollection.Recorded,
                    correlation: MetricCorrelation.For(recorded.RecordId.ToString()),
                    timestampUtc: at);
                break;

            case AuditArchived archived:
                Monitoring.Record(tenant, AuditMetricCollection.Archived, archived.Count, timestampUtc: at);
                break;

            case AuditRestored restored:
                Monitoring.Record(tenant, AuditMetricCollection.Restored, restored.Count, timestampUtc: at);
                break;

            case AuditRetentionExpired expired:
                Monitoring.Record(tenant, AuditMetricCollection.Expired, expired.Count, timestampUtc: at);
                break;

            case AuditExported exported:
                // Sliced by format, never by who exported: a label is a series identity, and filing metrics
                // under a person's id would both explode the cardinality and put an identity somewhere it has
                // no business being. Who exported what belongs in the audit trail, which already records it.
                Monitoring.Record(
                    tenant,
                    AuditMetricCollection.Exported,
                    exported.Count,
                    MetricDimension.Of(MetricLabel.Of(
                        Configuration.MonitoringConstants.OutcomeLabel, exported.Format)),
                    timestampUtc: at);
                break;

            default:
                break;
        }
    }
}
