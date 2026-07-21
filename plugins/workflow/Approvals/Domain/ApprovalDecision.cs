namespace FactoryOS.Plugins.Workflow.Approvals.Domain;

/// <summary>A single participant's cast vote.</summary>
/// <param name="ParticipantId">The participant who voted.</param>
/// <param name="Kind">Whether they approved or rejected.</param>
/// <param name="DecidedBy">The concrete user who cast the vote.</param>
/// <param name="Comment">An optional comment accompanying the vote.</param>
/// <param name="DecidedOnUtc">When the vote was cast.</param>
public sealed record ApprovalDecision(
    string ParticipantId,
    ApprovalDecisionKind Kind,
    string? DecidedBy,
    string? Comment,
    DateTimeOffset DecidedOnUtc);

/// <summary>
/// The live record of one participant's step within a running approval: who is expected to vote, at which
/// stage, their current status and the vote they cast if any.
/// </summary>
public sealed class ApprovalStep
{
    internal ApprovalStep(string participantId, ApprovalLevel level, string assignee, int weight)
    {
        ParticipantId = participantId;
        Level = level;
        Assignee = assignee;
        Weight = weight;
        Status = ApprovalParticipantStatus.Pending;
    }

    /// <summary>Gets the participant id.</summary>
    public string ParticipantId { get; }

    /// <summary>Gets the stage level the step belongs to.</summary>
    public ApprovalLevel Level { get; }

    /// <summary>Gets the resolved assignee expected to vote.</summary>
    public string Assignee { get; private set; }

    /// <summary>Gets the vote weight.</summary>
    public int Weight { get; }

    /// <summary>Gets the current status.</summary>
    public ApprovalParticipantStatus Status { get; private set; }

    /// <summary>Gets the cast vote, if any.</summary>
    public ApprovalDecision? Decision { get; private set; }

    internal void Record(ApprovalDecision decision)
    {
        Decision = decision;
        Status = decision.Kind == ApprovalDecisionKind.Approve
            ? ApprovalParticipantStatus.Approved
            : ApprovalParticipantStatus.Rejected;
    }

    internal void Skip()
    {
        if (Status == ApprovalParticipantStatus.Pending)
        {
            Status = ApprovalParticipantStatus.Skipped;
        }
    }

    internal void Reassign(string assignee) => Assignee = assignee;
}
