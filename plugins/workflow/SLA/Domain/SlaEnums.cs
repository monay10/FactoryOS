namespace FactoryOS.Plugins.Workflow.SLA.Domain;

/// <summary>The lifecycle state of an SLA instance.</summary>
public enum SlaStatus
{
    /// <summary>Running and inside its deadline.</summary>
    Active = 0,

    /// <summary>Suspended; business time does not accrue while paused.</summary>
    Paused = 1,

    /// <summary>The deadline passed while the work was still open. Still running — escalations continue.</summary>
    Breached = 2,

    /// <summary>The timeout limit passed; the SLA gave up waiting. Terminal.</summary>
    TimedOut = 3,

    /// <summary>The tracked work finished. Terminal.</summary>
    Completed = 4,

    /// <summary>The SLA was cancelled before the work finished. Terminal.</summary>
    Cancelled = 5,
}

/// <summary>
/// The terminal disposition of an SLA, kept distinct from <see cref="SlaStatus"/> so reports and KPIs can tell a
/// deadline that was met from one that was merely finished late, and both from a hard timeout.
/// </summary>
public enum SlaOutcome
{
    /// <summary>Not finished yet.</summary>
    None = 0,

    /// <summary>The work finished within the deadline.</summary>
    Met = 1,

    /// <summary>The work finished, but only after the deadline had passed.</summary>
    Breached = 2,

    /// <summary>The timeout limit passed before the work finished.</summary>
    TimedOut = 3,

    /// <summary>The SLA was cancelled.</summary>
    Cancelled = 4,
}

/// <summary>The kind of work an SLA tracks.</summary>
public enum SlaTargetKind
{
    /// <summary>A workflow activity node.</summary>
    WorkflowActivity = 0,

    /// <summary>A human task.</summary>
    HumanTask = 1,

    /// <summary>An approval.</summary>
    Approval = 2,

    /// <summary>A form submission.</summary>
    FormSubmission = 3,
}

/// <summary>How time is counted for an SLA.</summary>
public enum SlaCalendarKind
{
    /// <summary>Every hour counts — a 24x7 clock.</summary>
    Continuous = 0,

    /// <summary>Only working hours count, and holidays are skipped.</summary>
    BusinessCalendar = 1,
}

/// <summary>What a principal may do with an SLA.</summary>
[Flags]
public enum SlaPermission
{
    /// <summary>No rights.</summary>
    None = 0,

    /// <summary>May read the SLA and its history.</summary>
    View = 1,

    /// <summary>May start an SLA for a target.</summary>
    Create = 2,

    /// <summary>May cancel a running SLA.</summary>
    Cancel = 4,

    /// <summary>May override an SLA: pause, resume or move its deadline.</summary>
    Override = 8,
}

/// <summary>The action recorded on an SLA's audit history.</summary>
public enum SlaHistoryAction
{
    /// <summary>The SLA started.</summary>
    Started = 0,

    /// <summary>The SLA was paused.</summary>
    Paused = 1,

    /// <summary>The SLA was resumed.</summary>
    Resumed = 2,

    /// <summary>A reminder fired.</summary>
    ReminderTriggered = 3,

    /// <summary>An escalation fired.</summary>
    Escalated = 4,

    /// <summary>The deadline passed.</summary>
    Expired = 5,

    /// <summary>The timeout limit passed.</summary>
    TimedOut = 6,

    /// <summary>The tracked work finished.</summary>
    Completed = 7,

    /// <summary>The SLA was cancelled.</summary>
    Cancelled = 8,

    /// <summary>The SLA advanced to its next stage.</summary>
    StageAdvanced = 9,
}
