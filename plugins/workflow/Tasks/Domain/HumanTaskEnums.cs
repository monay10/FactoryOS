namespace FactoryOS.Plugins.Workflow.Tasks.Domain;

/// <summary>The lifecycle status of a <see cref="HumanTaskInstance"/>.</summary>
public enum HumanTaskStatus
{
    /// <summary>Created but not yet assigned.</summary>
    Created = 0,

    /// <summary>Assigned to a principal but not yet waiting for action.</summary>
    Assigned = 1,

    /// <summary>Assigned and waiting for the assignee to act (the workflow activity is paused).</summary>
    Waiting = 2,

    /// <summary>Opened by the assignee and being worked on.</summary>
    InProgress = 3,

    /// <summary>Completed with a decision; the workflow activity has advanced.</summary>
    Completed = 4,

    /// <summary>Rejected by the assignee; the workflow activity advanced with a rejection outcome.</summary>
    Rejected = 5,

    /// <summary>Cancelled before completion.</summary>
    Cancelled = 6,

    /// <summary>Expired because its deadline passed with no completion or escalation.</summary>
    Expired = 7,

    /// <summary>Escalated to another principal after its deadline; remains actionable.</summary>
    Escalated = 8,
}

/// <summary>The urgency of a human task.</summary>
public enum HumanTaskPriority
{
    /// <summary>Low urgency.</summary>
    Low = 0,

    /// <summary>Normal urgency (the default).</summary>
    Normal = 1,

    /// <summary>High urgency.</summary>
    High = 2,

    /// <summary>Critical urgency.</summary>
    Critical = 3,
}

/// <summary>A coarse classification of what a human task asks its assignee to do.</summary>
public enum HumanTaskCategory
{
    /// <summary>A general task with no specific classification.</summary>
    General = 0,

    /// <summary>An approval request.</summary>
    Approval = 1,

    /// <summary>A review request.</summary>
    Review = 2,

    /// <summary>A data-entry task.</summary>
    DataEntry = 3,

    /// <summary>A decision request.</summary>
    Decision = 4,
}

/// <summary>How a human task's assignee is chosen.</summary>
public enum AssignmentStrategy
{
    /// <summary>A specific user.</summary>
    User = 0,

    /// <summary>Any holder of a role (all become candidates).</summary>
    Role = 1,

    /// <summary>Any member of a group (all become candidates).</summary>
    Group = 2,

    /// <summary>A principal resolved at runtime from an expression.</summary>
    Dynamic = 3,

    /// <summary>The next candidate in rotation among a fixed set.</summary>
    RoundRobin = 4,

    /// <summary>The candidate with the fewest open tasks.</summary>
    LoadBalanced = 5,
}

/// <summary>The visibility of a <see cref="HumanTaskComment"/>.</summary>
public enum CommentVisibility
{
    /// <summary>Visible only to internal reviewers.</summary>
    Internal = 0,

    /// <summary>Visible to everyone with read access.</summary>
    Public = 1,
}

/// <summary>The decision outcome recorded when a human task is completed or rejected.</summary>
public enum HumanTaskOutcome
{
    /// <summary>Approved.</summary>
    Approved = 0,

    /// <summary>Rejected.</summary>
    Rejected = 1,

    /// <summary>Completed without an approve/reject semantic.</summary>
    Done = 2,
}

/// <summary>
/// The set of actions a principal may perform on a human task. A <c>[Flags]</c> enum, so a permission grant
/// can confer several actions at once.
/// </summary>
[Flags]
public enum HumanTaskPermission
{
    /// <summary>No permission.</summary>
    None = 0,

    /// <summary>May view the task.</summary>
    Read = 1,

    /// <summary>May edit the task's data and comments.</summary>
    Write = 2,

    /// <summary>May approve the task.</summary>
    Approve = 4,

    /// <summary>May reject the task.</summary>
    Reject = 8,

    /// <summary>May delegate the task to someone else while keeping ownership.</summary>
    Delegate = 16,

    /// <summary>May reassign the task to a new owner.</summary>
    Reassign = 32,

    /// <summary>May cancel the task.</summary>
    Cancel = 64,

    /// <summary>May complete the task.</summary>
    Complete = 128,

    /// <summary>Every permission.</summary>
    All = Read | Write | Approve | Reject | Delegate | Reassign | Cancel | Complete,
}
