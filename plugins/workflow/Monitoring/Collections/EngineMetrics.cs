using FactoryOS.Plugins.Workflow.Monitoring.Domain;

namespace FactoryOS.Plugins.Workflow.Monitoring.Collections;

/// <summary>
/// Builds the metric definitions each collection is made of. Keeping the shape in one place is what stops the
/// catalogue drifting into thirteen slightly different conventions.
/// </summary>
internal static class Metric
{
    internal const string CountUnit = "count";
    internal const string MillisecondUnit = "ms";

    internal static MetricDefinition Counter(
        string key, MetricCategory category, string description, params string[] dimensions) =>
        new(key, category, MetricKind.Counter, CountUnit, description) { Dimensions = dimensions };

    internal static MetricDefinition Duration(
        string key, MetricCategory category, string description, params string[] dimensions) =>
        new(key, category, MetricKind.Duration, MillisecondUnit, description) { Dimensions = dimensions };

    internal static MetricDefinition Gauge(
        string key, MetricCategory category, string unit, string description, params string[] dimensions) =>
        new(key, category, MetricKind.Gauge, unit, description) { Dimensions = dimensions };
}

/// <summary>What the workflow runtime is measured by.</summary>
public static class WorkflowMetricCollection
{
    /// <summary>Workflow instances that started.</summary>
    public const string InstancesStarted = "workflow.instance.started";

    /// <summary>Workflow instances that reached the end of their definition.</summary>
    public const string InstancesCompleted = "workflow.instance.completed";

    /// <summary>Workflow instances that failed.</summary>
    public const string InstancesFailed = "workflow.instance.failed";

    /// <summary>Workflow instances that were cancelled before finishing.</summary>
    public const string InstancesCancelled = "workflow.instance.cancelled";

    /// <summary>How long a workflow instance took from start to finish.</summary>
    public const string InstanceDuration = "workflow.instance.duration";

    /// <summary>Activities that completed.</summary>
    public const string ActivitiesCompleted = "workflow.activity.completed";

    /// <summary>Activities that failed.</summary>
    public const string ActivitiesFailed = "workflow.activity.failed";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(InstancesStarted, MetricCategory.Workflow, "Workflow instances that started.", "key"),
        Metric.Counter(InstancesCompleted, MetricCategory.Workflow, "Workflow instances that completed.", "key"),
        Metric.Counter(InstancesFailed, MetricCategory.Workflow, "Workflow instances that failed.", "key"),
        Metric.Counter(InstancesCancelled, MetricCategory.Workflow, "Workflow instances that were cancelled.", "key"),
        Metric.Duration(InstanceDuration, MetricCategory.Workflow, "Workflow instance run time.", "key"),
        Metric.Counter(ActivitiesCompleted, MetricCategory.Workflow, "Activities that completed.", "key"),
        Metric.Counter(ActivitiesFailed, MetricCategory.Workflow, "Activities that failed.", "key"),
    ];
}

/// <summary>What the forms engine is measured by.</summary>
public static class FormsMetricCollection
{
    /// <summary>Form definitions that were published.</summary>
    public const string DefinitionsPublished = "forms.definition.published";

    /// <summary>Form instances that were opened.</summary>
    public const string InstancesOpened = "forms.instance.opened";

    /// <summary>Form instances that were submitted.</summary>
    public const string InstancesSubmitted = "forms.instance.submitted";

    /// <summary>Form submissions that were approved.</summary>
    public const string InstancesApproved = "forms.instance.approved";

    /// <summary>Form submissions that were rejected.</summary>
    public const string InstancesRejected = "forms.instance.rejected";

    /// <summary>Form instances that were cancelled.</summary>
    public const string InstancesCancelled = "forms.instance.cancelled";

    /// <summary>How long a form took from being opened to being submitted.</summary>
    public const string FillDuration = "forms.instance.duration";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(DefinitionsPublished, MetricCategory.Form, "Form definitions published.", "key"),
        Metric.Counter(InstancesOpened, MetricCategory.Form, "Form instances opened.", "key"),
        Metric.Counter(InstancesSubmitted, MetricCategory.Form, "Form instances submitted.", "key"),
        Metric.Counter(InstancesApproved, MetricCategory.Form, "Form submissions approved.", "key"),
        Metric.Counter(InstancesRejected, MetricCategory.Form, "Form submissions rejected.", "key"),
        Metric.Counter(InstancesCancelled, MetricCategory.Form, "Form instances cancelled.", "key"),
        Metric.Duration(FillDuration, MetricCategory.Form, "Time from opening a form to submitting it.", "key"),
    ];
}

/// <summary>What the human task engine is measured by.</summary>
public static class HumanTaskMetricCollection
{
    /// <summary>Tasks that were created.</summary>
    public const string Created = "tasks.created";

    /// <summary>Tasks that were assigned to somebody.</summary>
    public const string Assigned = "tasks.assigned";

    /// <summary>Tasks that were completed.</summary>
    public const string Completed = "tasks.completed";

    /// <summary>Tasks that were rejected.</summary>
    public const string Rejected = "tasks.rejected";

    /// <summary>Tasks that were cancelled.</summary>
    public const string Cancelled = "tasks.cancelled";

    /// <summary>Tasks that expired unanswered.</summary>
    public const string Expired = "tasks.expired";

    /// <summary>Tasks that were escalated.</summary>
    public const string Escalated = "tasks.escalated";

    /// <summary>How long a task took from creation to a decision.</summary>
    public const string Duration = "tasks.duration";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(Created, MetricCategory.HumanTask, "Human tasks created.", "key"),
        Metric.Counter(Assigned, MetricCategory.HumanTask, "Human tasks assigned.", "key"),
        Metric.Counter(Completed, MetricCategory.HumanTask, "Human tasks completed.", "key"),
        Metric.Counter(Rejected, MetricCategory.HumanTask, "Human tasks rejected.", "key"),
        Metric.Counter(Cancelled, MetricCategory.HumanTask, "Human tasks cancelled.", "key"),
        Metric.Counter(Expired, MetricCategory.HumanTask, "Human tasks that expired unanswered.", "key"),
        Metric.Counter(Escalated, MetricCategory.HumanTask, "Human tasks escalated.", "key"),
        Metric.Duration(Duration, MetricCategory.HumanTask, "Time from creating a task to deciding it.", "key"),
    ];
}

/// <summary>What the approval engine is measured by.</summary>
public static class ApprovalMetricCollection
{
    /// <summary>Approvals that were created.</summary>
    public const string Created = "approvals.created";

    /// <summary>Individual approve decisions.</summary>
    public const string Approved = "approvals.approved";

    /// <summary>Individual reject decisions.</summary>
    public const string Rejected = "approvals.rejected";

    /// <summary>Approvals that reached a resolution.</summary>
    public const string Completed = "approvals.completed";

    /// <summary>Approvals that expired undecided.</summary>
    public const string Expired = "approvals.expired";

    /// <summary>Approvals that were escalated.</summary>
    public const string Escalated = "approvals.escalated";

    /// <summary>How long an approval took from creation to resolution.</summary>
    public const string Duration = "approvals.duration";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(Created, MetricCategory.Approval, "Approvals created.", "key"),
        Metric.Counter(Approved, MetricCategory.Approval, "Approve decisions recorded.", "key"),
        Metric.Counter(Rejected, MetricCategory.Approval, "Reject decisions recorded.", "key"),
        Metric.Counter(Completed, MetricCategory.Approval, "Approvals resolved.", "key", "outcome"),
        Metric.Counter(Expired, MetricCategory.Approval, "Approvals that expired undecided.", "key"),
        Metric.Counter(Escalated, MetricCategory.Approval, "Approvals escalated.", "key"),
        Metric.Duration(Duration, MetricCategory.Approval, "Time from creating an approval to resolving it.", "key"),
    ];
}

/// <summary>What the notification engine is measured by.</summary>
public static class NotificationMetricCollection
{
    /// <summary>Notifications that entered the queue.</summary>
    public const string Queued = "notifications.queued";

    /// <summary>Notifications a channel accepted.</summary>
    public const string Sent = "notifications.sent";

    /// <summary>Notifications confirmed delivered.</summary>
    public const string Delivered = "notifications.delivered";

    /// <summary>Delivery attempts that failed.</summary>
    public const string Failed = "notifications.failed";

    /// <summary>Notifications that exhausted their retries and were dead-lettered.</summary>
    public const string DeadLettered = "notifications.deadlettered";

    /// <summary>Delivery attempts that were retried.</summary>
    public const string Retried = "notifications.retried";

    /// <summary>Notifications suppressed by preference or quiet hours.</summary>
    public const string Suppressed = "notifications.suppressed";

    /// <summary>Notifications that expired before being delivered.</summary>
    public const string Expired = "notifications.expired";

    /// <summary>How long a notification took from being queued to being delivered.</summary>
    public const string DeliveryDuration = "notifications.duration";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(Queued, MetricCategory.Notification, "Notifications queued.", "channel"),
        Metric.Counter(Sent, MetricCategory.Notification, "Notifications accepted by a channel.", "channel"),
        Metric.Counter(Delivered, MetricCategory.Notification, "Notifications delivered.", "channel"),
        Metric.Counter(Failed, MetricCategory.Notification, "Notification delivery attempts that failed.", "channel"),
        Metric.Counter(DeadLettered, MetricCategory.Notification, "Notifications dead-lettered.", "channel"),
        Metric.Counter(Retried, MetricCategory.Notification, "Notification delivery attempts retried.", "channel"),
        Metric.Counter(Suppressed, MetricCategory.Notification, "Notifications suppressed.", "channel"),
        Metric.Counter(Expired, MetricCategory.Notification, "Notifications that expired undelivered.", "channel"),
        Metric.Duration(
            DeliveryDuration, MetricCategory.Notification, "Time from queuing a notification to delivering it.",
            "channel"),
    ];
}

/// <summary>What the SLA engine is measured by.</summary>
public static class SlaMetricCollection
{
    /// <summary>SLAs that started tracking work.</summary>
    public const string Started = "sla.started";

    /// <summary>SLA clocks that were paused.</summary>
    public const string Paused = "sla.paused";

    /// <summary>SLA clocks that were resumed.</summary>
    public const string Resumed = "sla.resumed";

    /// <summary>Reminders that fired ahead of a deadline.</summary>
    public const string Reminders = "sla.reminder";

    /// <summary>Escalation rungs that fired after a deadline.</summary>
    public const string Escalations = "sla.escalated";

    /// <summary>Deadlines that were missed while the work stayed open.</summary>
    public const string Breached = "sla.breached";

    /// <summary>SLAs that stopped waiting and finished as timed out.</summary>
    public const string TimedOut = "sla.timedout";

    /// <summary>SLAs that closed with their work finished.</summary>
    public const string Completed = "sla.completed";

    /// <summary>How long an SLA ran from starting to closing.</summary>
    public const string Duration = "sla.duration";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(Started, MetricCategory.Sla, "SLAs started.", "key"),
        Metric.Counter(Paused, MetricCategory.Sla, "SLA clocks paused.", "key"),
        Metric.Counter(Resumed, MetricCategory.Sla, "SLA clocks resumed.", "key"),
        Metric.Counter(Reminders, MetricCategory.Sla, "SLA reminders fired.", "key"),
        Metric.Counter(Escalations, MetricCategory.Sla, "SLA escalations fired.", "key"),
        Metric.Counter(Breached, MetricCategory.Sla, "SLA deadlines missed.", "key"),
        Metric.Counter(TimedOut, MetricCategory.Sla, "SLAs that timed out.", "key"),
        Metric.Counter(Completed, MetricCategory.Sla, "SLAs closed.", "key", "outcome"),
        Metric.Duration(Duration, MetricCategory.Sla, "Time an SLA ran from starting to closing.", "key"),
    ];
}

/// <summary>What the audit engine is measured by.</summary>
public static class AuditMetricCollection
{
    /// <summary>Records sealed into a tenant's chain.</summary>
    public const string Recorded = "audit.recorded";

    /// <summary>Records moved into the archive.</summary>
    public const string Archived = "audit.archived";

    /// <summary>Records brought back out of the archive.</summary>
    public const string Restored = "audit.restored";

    /// <summary>Records that outlived their retention.</summary>
    public const string Expired = "audit.expired";

    /// <summary>Records handed to an export.</summary>
    public const string Exported = "audit.exported";

    /// <summary>
    /// Chain verifications that found a broken link. This is the audit engine's real failure signal — records
    /// leaving the trail because a retention policy said so is the system working; a record that no longer
    /// hashes to what it claims is the system failing.
    /// </summary>
    public const string TamperDetections = "audit.tamper.detected";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(Recorded, MetricCategory.Audit, "Audit records sealed into a chain."),
        Metric.Counter(Archived, MetricCategory.Audit, "Audit records archived."),
        Metric.Counter(Restored, MetricCategory.Audit, "Audit records restored from the archive."),
        Metric.Counter(Expired, MetricCategory.Audit, "Audit records that outlived their retention."),
        Metric.Counter(Exported, MetricCategory.Audit, "Audit records exported.", "outcome"),
        Metric.Counter(
            TamperDetections, MetricCategory.Audit, "Chain verifications that found a broken link."),
    ];
}
