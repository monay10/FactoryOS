namespace FactoryOS.Plugins.Workflow.Approvals.Domain;

/// <summary>
/// A participant of an approval stage: who they are (an assignment) and how much their vote weighs for the
/// weighted-vote rule.
/// </summary>
/// <param name="Id">The participant id, unique within the definition.</param>
/// <param name="Assignment">Who the participant is.</param>
/// <param name="Weight">The vote weight (used by the weighted-vote rule; 1 by default).</param>
public sealed record ApprovalParticipant(string Id, ApprovalAssignment Assignment, int Weight = 1)
{
    /// <summary>Gets the vote weight.</summary>
    public int Weight { get; } = Weight >= 1
        ? Weight
        : throw new ArgumentOutOfRangeException(nameof(Weight), Weight, "A participant weight must be 1 or greater.");
}

/// <summary>
/// A stage of an approval: the participants who decide together and the policy that turns their votes into an
/// outcome. Stages run in <see cref="Level"/> order; a sequential approval has several stages, a parallel or
/// single approval has one.
/// </summary>
/// <param name="Level">The stage's level (its position in a sequential approval).</param>
/// <param name="Name">The stage name.</param>
/// <param name="Policy">The decision rule.</param>
/// <param name="Participants">The participants who decide at this stage.</param>
public sealed record ApprovalStage(
    ApprovalLevel Level, string Name, ApprovalPolicy Policy, IReadOnlyList<ApprovalParticipant> Participants);
