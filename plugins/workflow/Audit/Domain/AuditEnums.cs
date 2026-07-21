namespace FactoryOS.Plugins.Workflow.Audit.Domain;

/// <summary>
/// The coarse verb an audit record describes. The precise source event name is kept separately on
/// <see cref="AuditRecord.EventType"/>, so this stays a small, stable vocabulary that reports can group by
/// instead of growing a value per source event.
/// </summary>
public enum AuditAction
{
    /// <summary>Something was created.</summary>
    Created = 0,

    /// <summary>Something began running.</summary>
    Started = 1,

    /// <summary>Something was modified.</summary>
    Updated = 2,

    /// <summary>Something was opened for work.</summary>
    Opened = 3,

    /// <summary>Something was submitted for processing.</summary>
    Submitted = 4,

    /// <summary>Work was assigned to a principal.</summary>
    Assigned = 5,

    /// <summary>Something finished successfully.</summary>
    Completed = 6,

    /// <summary>A decision approved something.</summary>
    Approved = 7,

    /// <summary>A decision rejected something.</summary>
    Rejected = 8,

    /// <summary>Something was cancelled before finishing.</summary>
    Cancelled = 9,

    /// <summary>Something was escalated to another principal.</summary>
    Escalated = 10,

    /// <summary>A deadline passed.</summary>
    Expired = 11,

    /// <summary>A hard timeout ended the wait.</summary>
    TimedOut = 12,

    /// <summary>A clock or process was suspended.</summary>
    Paused = 13,

    /// <summary>A clock or process was restarted.</summary>
    Resumed = 14,

    /// <summary>Something failed.</summary>
    Failed = 15,

    /// <summary>Something was placed on a queue.</summary>
    Queued = 16,

    /// <summary>A message was handed to a transport.</summary>
    Sent = 17,

    /// <summary>A message reached its recipient.</summary>
    Delivered = 18,

    /// <summary>A recipient read something.</summary>
    Read = 19,

    /// <summary>Delivery was suppressed by a rule or preference.</summary>
    Suppressed = 20,

    /// <summary>A principal signed in.</summary>
    SignedIn = 21,

    /// <summary>A principal signed out.</summary>
    SignedOut = 22,

    /// <summary>Access to a resource was granted.</summary>
    AccessGranted = 23,

    /// <summary>Access to a resource was denied.</summary>
    AccessDenied = 24,

    /// <summary>Configuration was changed.</summary>
    Changed = 25,

    /// <summary>An operation was executed against an external system or plugin.</summary>
    Executed = 26,

    /// <summary>A record was moved to the archive.</summary>
    Archived = 27,

    /// <summary>A record was restored from the archive.</summary>
    Restored = 28,

    /// <summary>Records were exported.</summary>
    Exported = 29,

    /// <summary>A record passed its retention period and was removed.</summary>
    Deleted = 30,
}

/// <summary>Which part of the platform an audit record came from.</summary>
public enum AuditCategory
{
    /// <summary>The workflow runtime.</summary>
    Workflow = 0,

    /// <summary>The forms engine.</summary>
    Form = 1,

    /// <summary>The human task engine.</summary>
    HumanTask = 2,

    /// <summary>The approval engine.</summary>
    Approval = 3,

    /// <summary>The notification engine.</summary>
    Notification = 4,

    /// <summary>The SLA engine.</summary>
    Sla = 5,

    /// <summary>Sign-in and sign-out.</summary>
    Authentication = 6,

    /// <summary>Access decisions.</summary>
    Authorization = 7,

    /// <summary>Configuration changes.</summary>
    Configuration = 8,

    /// <summary>Connector operations against outside systems.</summary>
    Connector = 9,

    /// <summary>Plugin lifecycle operations.</summary>
    Plugin = 10,

    /// <summary>The audit engine's own housekeeping.</summary>
    System = 11,
}

/// <summary>How much attention an audit record deserves.</summary>
public enum AuditSeverity
{
    /// <summary>Routine activity.</summary>
    Info = 0,

    /// <summary>Notable but not a problem.</summary>
    Notice = 1,

    /// <summary>Something went wrong or a limit was missed.</summary>
    Warning = 2,

    /// <summary>A serious failure or a security-relevant denial.</summary>
    Critical = 3,
}

/// <summary>Whether the audited operation succeeded.</summary>
public enum AuditResult
{
    /// <summary>The operation succeeded.</summary>
    Success = 0,

    /// <summary>The operation failed.</summary>
    Failure = 1,

    /// <summary>The operation was refused by a policy.</summary>
    Denied = 2,
}

/// <summary>The kind of entity an audit record is about.</summary>
public enum AuditTargetType
{
    /// <summary>A workflow instance.</summary>
    Workflow = 0,

    /// <summary>A form or form instance.</summary>
    Form = 1,

    /// <summary>A human task.</summary>
    Task = 2,

    /// <summary>An approval.</summary>
    Approval = 3,

    /// <summary>A notification.</summary>
    Notification = 4,

    /// <summary>A service-level agreement.</summary>
    Sla = 5,

    /// <summary>A connector.</summary>
    Connector = 6,

    /// <summary>A user.</summary>
    User = 7,

    /// <summary>A role.</summary>
    Role = 8,

    /// <summary>A tenant.</summary>
    Tenant = 9,

    /// <summary>An organization.</summary>
    Organization = 10,

    /// <summary>A device or edge gateway.</summary>
    Device = 11,

    /// <summary>A plugin.</summary>
    Plugin = 12,

    /// <summary>A configuration section.</summary>
    Configuration = 13,
}

/// <summary>What kind of principal performed an audited operation.</summary>
public enum AuditActorKind
{
    /// <summary>A human user.</summary>
    User = 0,

    /// <summary>The platform itself, with no user behind the action.</summary>
    System = 1,

    /// <summary>A background service or scheduler.</summary>
    Service = 2,

    /// <summary>A plugin acting on its own behalf.</summary>
    Plugin = 3,

    /// <summary>An external system reached through a connector.</summary>
    External = 4,
}

/// <summary>What a retention policy does to a record once its period has passed.</summary>
public enum AuditRetentionAction
{
    /// <summary>Move the record to the archive, where it stays readable.</summary>
    Archive = 0,

    /// <summary>Remove the record permanently.</summary>
    Delete = 1,
}

/// <summary>What a principal may do with audit records.</summary>
[Flags]
public enum AuditPermission
{
    /// <summary>No rights.</summary>
    None = 0,

    /// <summary>May search and read audit records.</summary>
    ViewAudit = 1,

    /// <summary>May export audit records.</summary>
    ExportAudit = 2,

    /// <summary>May move records to the archive.</summary>
    ArchiveAudit = 4,

    /// <summary>May restore records from the archive.</summary>
    RestoreAudit = 8,
}
