using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.SLA.Events;
using FactoryOS.Plugins.Workflow.Tasks.Events;

namespace FactoryOS.Plugins.Workflow.Audit.Sources;

/// <summary>
/// Turns the workflow runtime's events into audit records. Reads the runtime's published events only — the
/// runtime is never modified and never learns that an audit trail exists.
/// </summary>
public sealed class WorkflowAuditSubscriber : IWorkflowEventSink
{
    private readonly AuditEngine _audit;

    /// <summary>Initializes a new instance of the <see cref="WorkflowAuditSubscriber"/> class.</summary>
    /// <param name="audit">The audit engine.</param>
    public WorkflowAuditSubscriber(AuditEngine audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        _audit = audit;
    }

    /// <inheritdoc />
    public void Publish(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        var (action, severity, result, key, message) = workflowEvent switch
        {
            WorkflowStarted started => (AuditAction.Started, AuditSeverity.Info, AuditResult.Success,
                started.DefinitionKey, $"Workflow '{started.DefinitionKey}' started."),
            WorkflowCompleted => (AuditAction.Completed, AuditSeverity.Info, AuditResult.Success,
                workflowEvent.InstanceId.ToString(), "Workflow completed."),
            WorkflowCancelled => (AuditAction.Cancelled, AuditSeverity.Notice, AuditResult.Success,
                workflowEvent.InstanceId.ToString(), "Workflow cancelled."),
            WorkflowFailed failed => (AuditAction.Failed, AuditSeverity.Warning, AuditResult.Failure,
                workflowEvent.InstanceId.ToString(), $"Workflow failed: {failed.Reason}"),
            ActivityStarted activity => (AuditAction.Started, AuditSeverity.Info, AuditResult.Success,
                activity.NodeId, $"Activity '{activity.NodeId}' started."),
            ActivityCompleted activity => (AuditAction.Completed, AuditSeverity.Info, AuditResult.Success,
                activity.NodeId, $"Activity '{activity.NodeId}' completed."),
            ActivityFailed activity => (AuditAction.Failed, AuditSeverity.Warning, AuditResult.Failure,
                activity.NodeId, $"Activity '{activity.NodeId}' failed: {activity.Reason}"),
            _ => (AuditAction.Updated, AuditSeverity.Info, AuditResult.Success,
                workflowEvent.InstanceId.ToString(), workflowEvent.GetType().Name),
        };

        _audit.Record(new AuditEntry
        {
            Category = AuditCategory.Workflow,
            Action = action,
            Severity = severity,
            Result = result,
            Target = new AuditTarget(AuditTargetType.Workflow, key, workflowEvent.InstanceId.ToString()),
            Scope = new AuditScope(workflowEvent.Tenant, Module: "workflow"),
            Correlation = AuditCorrelation.For(workflowEvent.InstanceId.ToString()),
            EventType = workflowEvent.GetType().Name,
            Message = message,
            OccurredOnUtc = workflowEvent.OccurredOnUtc,
        });
    }
}

/// <summary>Turns the forms engine's events into audit records, without modifying that engine.</summary>
public sealed class FormsAuditSubscriber : IFormEventSink
{
    private readonly AuditEngine _audit;

    /// <summary>Initializes a new instance of the <see cref="FormsAuditSubscriber"/> class.</summary>
    /// <param name="audit">The audit engine.</param>
    public FormsAuditSubscriber(AuditEngine audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        _audit = audit;
    }

    /// <inheritdoc />
    public void Publish(FormEvent formEvent)
    {
        ArgumentNullException.ThrowIfNull(formEvent);

        var (action, severity, actor, message) = formEvent switch
        {
            FormCreated => (AuditAction.Created, AuditSeverity.Info, AuditActor.System,
                $"Form '{formEvent.FormKey}' created."),
            FormUpdated => (AuditAction.Updated, AuditSeverity.Info, AuditActor.System,
                $"Form '{formEvent.FormKey}' updated."),
            FormPublished => (AuditAction.Updated, AuditSeverity.Notice, AuditActor.System,
                $"Form '{formEvent.FormKey}' published."),
            FormOpened opened => (AuditAction.Opened, AuditSeverity.Info,
                opened.Assignee is null ? AuditActor.System : AuditActor.User(opened.Assignee),
                $"Form '{formEvent.FormKey}' opened."),
            FormSaved => (AuditAction.Updated, AuditSeverity.Info, AuditActor.System,
                $"Draft saved on form '{formEvent.FormKey}'."),
            FormSubmitted submitted => (AuditAction.Submitted, AuditSeverity.Notice,
                submitted.SubmittedBy is null ? AuditActor.System : AuditActor.User(submitted.SubmittedBy),
                $"Form '{formEvent.FormKey}' submitted."),
            FormApproved => (AuditAction.Approved, AuditSeverity.Notice, AuditActor.System,
                $"Form '{formEvent.FormKey}' approved."),
            FormRejected rejected => (AuditAction.Rejected, AuditSeverity.Notice, AuditActor.System,
                $"Form '{formEvent.FormKey}' rejected: {rejected.Reason}"),
            FormCancelled => (AuditAction.Cancelled, AuditSeverity.Notice, AuditActor.System,
                $"Form '{formEvent.FormKey}' cancelled."),
            _ => (AuditAction.Updated, AuditSeverity.Info, AuditActor.System, formEvent.GetType().Name),
        };

        var instanceId = formEvent is FormInstanceEvent instance ? instance.InstanceId.ToString() : null;

        _audit.Record(new AuditEntry
        {
            Category = AuditCategory.Form,
            Action = action,
            Severity = severity,
            Actor = actor,
            Target = new AuditTarget(AuditTargetType.Form, formEvent.FormKey, instanceId),
            Scope = new AuditScope(formEvent.Tenant, Module: "forms"),
            Correlation = AuditCorrelation.For(instanceId ?? formEvent.FormKey),
            EventType = formEvent.GetType().Name,
            Message = message,
            OccurredOnUtc = formEvent.OccurredOnUtc,
        });
    }
}

/// <summary>Turns the human task engine's events into audit records, without modifying that engine.</summary>
public sealed class HumanTaskAuditSubscriber : IHumanTaskEventSink
{
    private readonly AuditEngine _audit;

    /// <summary>Initializes a new instance of the <see cref="HumanTaskAuditSubscriber"/> class.</summary>
    /// <param name="audit">The audit engine.</param>
    public HumanTaskAuditSubscriber(AuditEngine audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        _audit = audit;
    }

    /// <inheritdoc />
    public void Publish(HumanTaskEvent taskEvent)
    {
        ArgumentNullException.ThrowIfNull(taskEvent);

        var (action, severity, actor, message) = taskEvent switch
        {
            HumanTaskCreated => (AuditAction.Created, AuditSeverity.Info, AuditActor.System,
                $"Task '{taskEvent.DefinitionKey}' created."),
            HumanTaskAssigned assigned => (AuditAction.Assigned, AuditSeverity.Info, AuditActor.System,
                $"Task '{taskEvent.DefinitionKey}' assigned to {assigned.Assignee}."),
            HumanTaskOpened => (AuditAction.Opened, AuditSeverity.Info, AuditActor.System,
                $"Task '{taskEvent.DefinitionKey}' opened."),
            HumanTaskCompleted completed => (AuditAction.Completed, AuditSeverity.Notice,
                completed.CompletedBy is null ? AuditActor.System : AuditActor.User(completed.CompletedBy),
                $"Task '{taskEvent.DefinitionKey}' completed."),
            HumanTaskRejected rejected => (AuditAction.Rejected, AuditSeverity.Notice,
                rejected.RejectedBy is null ? AuditActor.System : AuditActor.User(rejected.RejectedBy),
                $"Task '{taskEvent.DefinitionKey}' rejected."),
            HumanTaskCancelled => (AuditAction.Cancelled, AuditSeverity.Notice, AuditActor.System,
                $"Task '{taskEvent.DefinitionKey}' cancelled."),
            HumanTaskExpired => (AuditAction.Expired, AuditSeverity.Warning, AuditActor.System,
                $"Task '{taskEvent.DefinitionKey}' expired."),
            HumanTaskEscalated escalated => (AuditAction.Escalated, AuditSeverity.Warning, AuditActor.System,
                $"Task '{taskEvent.DefinitionKey}' escalated to {escalated.Assignee} (level {escalated.Level})."),
            HumanTaskReassigned reassigned => (AuditAction.Assigned, AuditSeverity.Notice, AuditActor.System,
                $"Task '{taskEvent.DefinitionKey}' reassigned to {reassigned.Assignee}."),
            _ => (AuditAction.Updated, AuditSeverity.Info, AuditActor.System, taskEvent.GetType().Name),
        };

        _audit.Record(new AuditEntry
        {
            Category = AuditCategory.HumanTask,
            Action = action,
            Severity = severity,
            Actor = actor,
            Target = AuditTarget.Of(AuditTargetType.Task, taskEvent.DefinitionKey, taskEvent.TaskId),
            Scope = new AuditScope(taskEvent.Tenant, Module: "tasks"),
            Correlation = AuditCorrelation.For(taskEvent.TaskId.ToString()),
            EventType = taskEvent.GetType().Name,
            Message = message,
            OccurredOnUtc = taskEvent.OccurredOnUtc,
        });
    }
}

/// <summary>Turns the approval engine's events into audit records, without modifying that engine.</summary>
public sealed class ApprovalAuditSubscriber : IApprovalEventSink
{
    private readonly AuditEngine _audit;

    /// <summary>Initializes a new instance of the <see cref="ApprovalAuditSubscriber"/> class.</summary>
    /// <param name="audit">The audit engine.</param>
    public ApprovalAuditSubscriber(AuditEngine audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        _audit = audit;
    }

    /// <inheritdoc />
    public void Publish(ApprovalEvent approvalEvent)
    {
        ArgumentNullException.ThrowIfNull(approvalEvent);

        var (action, severity, actor, message) = approvalEvent switch
        {
            ApprovalCreated => (AuditAction.Created, AuditSeverity.Info, AuditActor.System,
                $"Approval '{approvalEvent.DefinitionKey}' created."),
            ApprovalStarted => (AuditAction.Started, AuditSeverity.Info, AuditActor.System,
                $"Approval '{approvalEvent.DefinitionKey}' started."),
            ApprovalAssigned assigned => (AuditAction.Assigned, AuditSeverity.Info, AuditActor.System,
                $"Approval '{approvalEvent.DefinitionKey}' assigned to {assigned.Assignee}."),
            ApprovalApproved approved => (AuditAction.Approved, AuditSeverity.Notice,
                approved.DecidedBy is null ? AuditActor.System : AuditActor.User(approved.DecidedBy),
                $"{approved.ParticipantId} approved '{approvalEvent.DefinitionKey}'."),
            ApprovalRejected rejected => (AuditAction.Rejected, AuditSeverity.Notice,
                rejected.DecidedBy is null ? AuditActor.System : AuditActor.User(rejected.DecidedBy),
                $"{rejected.ParticipantId} rejected '{approvalEvent.DefinitionKey}'."),
            ApprovalCompleted completed => (AuditAction.Completed, AuditSeverity.Notice, AuditActor.System,
                $"Approval '{approvalEvent.DefinitionKey}' completed as {completed.Resolution}."),
            ApprovalCancelled => (AuditAction.Cancelled, AuditSeverity.Notice, AuditActor.System,
                $"Approval '{approvalEvent.DefinitionKey}' cancelled."),
            ApprovalExpired => (AuditAction.Expired, AuditSeverity.Warning, AuditActor.System,
                $"Approval '{approvalEvent.DefinitionKey}' expired."),
            ApprovalEscalated escalated => (AuditAction.Escalated, AuditSeverity.Warning, AuditActor.System,
                $"Approval '{approvalEvent.DefinitionKey}' escalated to {escalated.Assignee}."),
            ApprovalReminderSent => (AuditAction.Sent, AuditSeverity.Info, AuditActor.System,
                $"Reminder sent for approval '{approvalEvent.DefinitionKey}'."),
            _ => (AuditAction.Updated, AuditSeverity.Info, AuditActor.System, approvalEvent.GetType().Name),
        };

        _audit.Record(new AuditEntry
        {
            Category = AuditCategory.Approval,
            Action = action,
            Severity = severity,
            Actor = actor,
            Target = AuditTarget.Of(AuditTargetType.Approval, approvalEvent.DefinitionKey, approvalEvent.ApprovalId),
            Scope = new AuditScope(approvalEvent.Tenant, Module: "approvals"),
            Correlation = AuditCorrelation.For(approvalEvent.ApprovalId.ToString()),
            EventType = approvalEvent.GetType().Name,
            Message = message,
            OccurredOnUtc = approvalEvent.OccurredOnUtc,
        });
    }
}

/// <summary>Turns the notification engine's events into audit records, without modifying that engine.</summary>
public sealed class NotificationAuditSubscriber : INotificationEventSink
{
    private readonly AuditEngine _audit;

    /// <summary>Initializes a new instance of the <see cref="NotificationAuditSubscriber"/> class.</summary>
    /// <param name="audit">The audit engine.</param>
    public NotificationAuditSubscriber(AuditEngine audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        _audit = audit;
    }

    /// <inheritdoc />
    public void Publish(NotificationEvent notificationEvent)
    {
        ArgumentNullException.ThrowIfNull(notificationEvent);

        var (action, severity, result, message) = notificationEvent switch
        {
            NotificationQueued => (AuditAction.Queued, AuditSeverity.Info, AuditResult.Success,
                "Notification queued."),
            NotificationSending => (AuditAction.Sent, AuditSeverity.Info, AuditResult.Success,
                "Notification handed to its channel."),
            NotificationSent => (AuditAction.Sent, AuditSeverity.Info, AuditResult.Success,
                "Notification accepted by its channel."),
            NotificationDelivered => (AuditAction.Delivered, AuditSeverity.Info, AuditResult.Success,
                "Notification delivered."),
            NotificationRead => (AuditAction.Read, AuditSeverity.Info, AuditResult.Success,
                "Notification read."),
            NotificationFailed failed => (AuditAction.Failed,
                failed.DeadLettered ? AuditSeverity.Warning : AuditSeverity.Notice, AuditResult.Failure,
                failed.DeadLettered
                    ? $"Notification dead-lettered after {failed.AttemptNumber} attempt(s): {failed.Reason}."
                    : $"Notification attempt {failed.AttemptNumber} failed: {failed.Reason}."),
            NotificationRetried retried => (AuditAction.Queued, AuditSeverity.Notice, AuditResult.Success,
                $"Notification retry scheduled after attempt {retried.AttemptNumber}."),
            NotificationCancelled => (AuditAction.Cancelled, AuditSeverity.Notice, AuditResult.Success,
                "Notification cancelled."),
            NotificationExpired => (AuditAction.Expired, AuditSeverity.Notice, AuditResult.Failure,
                "Notification expired undelivered."),
            NotificationSuppressed => (AuditAction.Suppressed, AuditSeverity.Info, AuditResult.Success,
                "Notification suppressed."),
            _ => (AuditAction.Updated, AuditSeverity.Info, AuditResult.Success, notificationEvent.GetType().Name),
        };

        _audit.Record(new AuditEntry
        {
            Category = AuditCategory.Notification,
            Action = action,
            Severity = severity,
            Result = result,
            Target = AuditTarget.Of(
                AuditTargetType.Notification, notificationEvent.Channel.ToString(), notificationEvent.NotificationId),
            Scope = new AuditScope(notificationEvent.Tenant, Module: "notifications"),
            Correlation = AuditCorrelation.For(notificationEvent.NotificationId.ToString()),
            EventType = notificationEvent.GetType().Name,
            Message = message,
            OccurredOnUtc = notificationEvent.OccurredOnUtc,
        });
    }
}

/// <summary>
/// Turns the SLA engine's events into audit records. The SLA event seam already fans out, so this subscriber
/// simply registers alongside the existing consumers rather than displacing any of them.
/// </summary>
public sealed class SlaAuditSubscriber : ISlaEventSink
{
    private readonly AuditEngine _audit;

    /// <summary>Initializes a new instance of the <see cref="SlaAuditSubscriber"/> class.</summary>
    /// <param name="audit">The audit engine.</param>
    public SlaAuditSubscriber(AuditEngine audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        _audit = audit;
    }

    /// <inheritdoc />
    public void Publish(SlaEvent slaEvent)
    {
        ArgumentNullException.ThrowIfNull(slaEvent);

        var (action, severity, result, message) = slaEvent switch
        {
            SlaStarted started => (AuditAction.Started, AuditSeverity.Info, AuditResult.Success,
                $"SLA '{slaEvent.DefinitionKey}' started; due {started.DueOnUtc:O}."),
            SlaPaused paused => (AuditAction.Paused, AuditSeverity.Notice, AuditResult.Success,
                $"SLA '{slaEvent.DefinitionKey}' paused ({paused.Reason.Code})."),
            SlaResumed resumed => (AuditAction.Resumed, AuditSeverity.Notice, AuditResult.Success,
                $"SLA '{slaEvent.DefinitionKey}' resumed ({resumed.Reason.Code}); now due {resumed.DueOnUtc:O}."),
            SlaReminderTriggered => (AuditAction.Sent, AuditSeverity.Info, AuditResult.Success,
                $"SLA reminder fired for '{slaEvent.DefinitionKey}'."),
            SlaEscalated escalated => (AuditAction.Escalated, AuditSeverity.Warning, AuditResult.Success,
                $"SLA '{slaEvent.DefinitionKey}' escalated to {escalated.Assignee} (level {escalated.Level})."),
            SlaExpired => (AuditAction.Expired, AuditSeverity.Warning, AuditResult.Failure,
                $"SLA '{slaEvent.DefinitionKey}' breached its deadline."),
            SlaTimedOut => (AuditAction.TimedOut, AuditSeverity.Critical, AuditResult.Failure,
                $"SLA '{slaEvent.DefinitionKey}' timed out."),
            SlaCompleted completed => (AuditAction.Completed, AuditSeverity.Info, AuditResult.Success,
                $"SLA '{slaEvent.DefinitionKey}' completed as {completed.Outcome}."),
            SlaCancelled => (AuditAction.Cancelled, AuditSeverity.Notice, AuditResult.Success,
                $"SLA '{slaEvent.DefinitionKey}' cancelled."),
            _ => (AuditAction.Updated, AuditSeverity.Info, AuditResult.Success, slaEvent.GetType().Name),
        };

        _audit.Record(new AuditEntry
        {
            Category = AuditCategory.Sla,
            Action = action,
            Severity = severity,
            Result = result,
            Target = AuditTarget.Of(AuditTargetType.Sla, slaEvent.DefinitionKey, slaEvent.SlaId),
            Scope = new AuditScope(slaEvent.Tenant, Module: "sla"),
            // The SLA's own target is what ties its trail to the work it tracks.
            Correlation = new AuditCorrelation(
                slaEvent.Target.Id?.ToString() ?? slaEvent.SlaId.ToString(),
                CausationId: slaEvent.SlaId.ToString()),
            EventType = slaEvent.GetType().Name,
            Message = message,
            OccurredOnUtc = slaEvent.OccurredOnUtc,
        });
    }
}
