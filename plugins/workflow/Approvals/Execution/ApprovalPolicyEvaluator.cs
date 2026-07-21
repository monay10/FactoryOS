using FactoryOS.Plugins.Workflow.Approvals.Domain;

namespace FactoryOS.Plugins.Workflow.Approvals.Execution;

/// <summary>
/// Turns the votes cast on a stage into an outcome according to the stage's policy. All rules are evaluated
/// eagerly: a stage resolves to <see cref="ApprovalOutcome.Approved"/> or <see cref="ApprovalOutcome.Rejected"/>
/// as soon as the pending votes can no longer change the result, otherwise it stays
/// <see cref="ApprovalOutcome.Pending"/>. Pure — it reads the steps and returns an outcome.
/// </summary>
public sealed class ApprovalPolicyEvaluator
{
    /// <summary>Evaluates a stage's steps against its policy.</summary>
    /// <param name="policy">The stage policy.</param>
    /// <param name="steps">The stage's steps.</param>
    /// <returns>The stage outcome.</returns>
    public ApprovalOutcome Evaluate(ApprovalPolicy policy, IReadOnlyList<ApprovalStep> steps)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
        {
            return ApprovalOutcome.Pending;
        }

        var total = steps.Count;
        var approved = steps.Count(step => step.Status == ApprovalParticipantStatus.Approved);
        var rejected = steps.Count(step => step.Status == ApprovalParticipantStatus.Rejected);
        var pending = steps.Count(step => step.Status == ApprovalParticipantStatus.Pending);

        return policy.Kind switch
        {
            ApprovalPolicyKind.Single => Single(approved, rejected),
            ApprovalPolicyKind.AnyApprover => ByCount(approved, pending, needed: 1),
            ApprovalPolicyKind.AllApprovers or ApprovalPolicyKind.Consensus => ByCount(approved, pending, needed: total),
            ApprovalPolicyKind.Majority => ByCount(approved, pending, needed: (total / 2) + 1),
            ApprovalPolicyKind.Percentage => ByCount(approved, pending, needed: PercentageNeeded(total, policy.Percentage!.Value)),
            ApprovalPolicyKind.WeightedVote => ByWeight(steps, policy.WeightThreshold!.Value),
            ApprovalPolicyKind.FirstResponse => FirstResponse(steps),
            _ => ApprovalOutcome.Pending,
        };
    }

    private static ApprovalOutcome Single(int approved, int rejected)
    {
        if (approved >= 1)
        {
            return ApprovalOutcome.Approved;
        }

        return rejected >= 1 ? ApprovalOutcome.Rejected : ApprovalOutcome.Pending;
    }

    private static ApprovalOutcome ByCount(int approved, int pending, int needed)
    {
        if (approved >= needed)
        {
            return ApprovalOutcome.Approved;
        }

        // If even every remaining pending vote approving cannot reach the threshold, the stage is rejected.
        return approved + pending < needed ? ApprovalOutcome.Rejected : ApprovalOutcome.Pending;
    }

    private static ApprovalOutcome ByWeight(IReadOnlyList<ApprovalStep> steps, int threshold)
    {
        var approvedWeight = steps
            .Where(step => step.Status == ApprovalParticipantStatus.Approved).Sum(step => step.Weight);
        if (approvedWeight >= threshold)
        {
            return ApprovalOutcome.Approved;
        }

        var pendingWeight = steps
            .Where(step => step.Status == ApprovalParticipantStatus.Pending).Sum(step => step.Weight);
        return approvedWeight + pendingWeight < threshold ? ApprovalOutcome.Rejected : ApprovalOutcome.Pending;
    }

    private static ApprovalOutcome FirstResponse(IReadOnlyList<ApprovalStep> steps)
    {
        var first = steps
            .Where(step => step.Decision is not null)
            .OrderBy(step => step.Decision!.DecidedOnUtc)
            .FirstOrDefault();
        if (first is null)
        {
            return ApprovalOutcome.Pending;
        }

        return first.Decision!.Kind == ApprovalDecisionKind.Approve
            ? ApprovalOutcome.Approved
            : ApprovalOutcome.Rejected;
    }

    private static int PercentageNeeded(int total, double percentage) =>
        Math.Max(1, (int)Math.Ceiling(total * percentage / 100.0));
}
