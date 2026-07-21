namespace FactoryOS.Plugins.Workflow.Approvals.Domain;

/// <summary>The lifecycle status of an <see cref="ApprovalInstance"/>.</summary>
public enum ApprovalStatus
{
    /// <summary>Created but not yet started.</summary>
    Created = 0,

    /// <summary>Started and awaiting participant decisions.</summary>
    InProgress = 1,

    /// <summary>Finished with an approval.</summary>
    Approved = 2,

    /// <summary>Finished with a rejection.</summary>
    Rejected = 3,

    /// <summary>Cancelled before completion.</summary>
    Cancelled = 4,

    /// <summary>Expired because its deadline passed without a decision.</summary>
    Expired = 5,
}

/// <summary>The decision result of an approval or one of its stages.</summary>
public enum ApprovalOutcome
{
    /// <summary>Not yet decided.</summary>
    Pending = 0,

    /// <summary>Approved.</summary>
    Approved = 1,

    /// <summary>Rejected.</summary>
    Rejected = 2,
}

/// <summary>A single participant's vote.</summary>
public enum ApprovalDecisionKind
{
    /// <summary>The participant approves.</summary>
    Approve = 0,

    /// <summary>The participant rejects.</summary>
    Reject = 1,
}

/// <summary>The state of one participant's step within an approval.</summary>
public enum ApprovalParticipantStatus
{
    /// <summary>Awaiting the participant's decision.</summary>
    Pending = 0,

    /// <summary>The participant approved.</summary>
    Approved = 1,

    /// <summary>The participant rejected.</summary>
    Rejected = 2,

    /// <summary>The step was skipped (its stage resolved before the participant acted).</summary>
    Skipped = 3,
}

/// <summary>The overall shape of an approval.</summary>
public enum ApprovalStructure
{
    /// <summary>One participant decides.</summary>
    Single = 0,

    /// <summary>Stages decide in order; a later stage only starts once the earlier one is approved.</summary>
    Sequential = 1,

    /// <summary>Every participant of a single stage decides concurrently.</summary>
    Parallel = 2,
}

/// <summary>The decision rule a stage uses to turn its participants' votes into an outcome.</summary>
public enum ApprovalPolicyKind
{
    /// <summary>Exactly one participant; the stage takes that participant's decision.</summary>
    Single = 0,

    /// <summary>Approved as soon as any participant approves; rejected only if all reject.</summary>
    AnyApprover = 1,

    /// <summary>Approved only when every participant approves; rejected as soon as any rejects.</summary>
    AllApprovers = 2,

    /// <summary>Approved when more than half approve; rejected when a majority approval becomes impossible.</summary>
    Majority = 3,

    /// <summary>Approved when at least a configured percentage approve.</summary>
    Percentage = 4,

    /// <summary>Approved when the approving participants' weight reaches a configured threshold.</summary>
    WeightedVote = 5,

    /// <summary>Approved only when all approve with none against (unanimous consensus).</summary>
    Consensus = 6,

    /// <summary>The first participant to respond decides the stage.</summary>
    FirstResponse = 7,
}

/// <summary>The kind of subject an <see cref="ApprovalPermissionGrant"/> targets.</summary>
public enum ApprovalPrincipalKind
{
    /// <summary>A specific user.</summary>
    User = 0,

    /// <summary>A role.</summary>
    Role = 1,

    /// <summary>A group.</summary>
    Group = 2,
}

/// <summary>The set of actions a principal may perform on an approval.</summary>
[Flags]
public enum ApprovalPermission
{
    /// <summary>No permission.</summary>
    None = 0,

    /// <summary>May view the approval.</summary>
    View = 1,

    /// <summary>May cast an approve vote.</summary>
    Approve = 2,

    /// <summary>May cast a reject vote.</summary>
    Reject = 4,

    /// <summary>May cancel the approval.</summary>
    Cancel = 8,

    /// <summary>May delegate a vote to someone else.</summary>
    Delegate = 16,

    /// <summary>May reassign a participant step to a new owner.</summary>
    Reassign = 32,

    /// <summary>May comment on the approval.</summary>
    Comment = 64,

    /// <summary>Every permission.</summary>
    All = View | Approve | Reject | Cancel | Delegate | Reassign | Comment,
}
