using FactoryOS.Plugins.Workflow.Approvals.Domain;

namespace FactoryOS.Plugins.Workflow.Approvals.Execution;

/// <summary>
/// Resolves a stage's participants into concrete steps: each participant's assignment is evaluated against the
/// approval's context values to yield an assignee reference, and a pending step is created for it. Dynamic
/// assignments are resolved here so later stages pick up values produced by earlier ones.
/// </summary>
public sealed class ParticipantResolver
{
    /// <summary>Resolves a stage's participants into pending steps.</summary>
    /// <param name="stage">The stage to resolve.</param>
    /// <param name="values">The approval context values.</param>
    /// <returns>The resolved pending steps.</returns>
    public IReadOnlyList<ApprovalStep> Resolve(ApprovalStage stage, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(values);

        var steps = new List<ApprovalStep>(stage.Participants.Count);
        foreach (var participant in stage.Participants)
        {
            var assignee = participant.Assignment.Resolve(values);
            steps.Add(new ApprovalStep(participant.Id, stage.Level, assignee, participant.Weight));
        }

        return steps;
    }
}
