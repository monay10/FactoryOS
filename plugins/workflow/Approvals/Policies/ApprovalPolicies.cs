using FactoryOS.Plugins.Workflow.Approvals.Domain;

namespace FactoryOS.Plugins.Workflow.Approvals.Policies;

/// <summary>
/// Named shortcuts for the approval policies the engine supports, so callers can express intent directly. The
/// structural shapes — single, sequential and parallel — are expressed through the stages a definition holds
/// (one single-participant stage; several stages in order; one multi-participant stage). The decision rules —
/// any, all, majority, percentage, weighted vote, consensus and first response — are stage-level
/// <see cref="ApprovalPolicy"/> values.
/// </summary>
public static class ApprovalPolicies
{
    /// <summary>The single-approver decision rule.</summary>
    public static ApprovalPolicy Single => ApprovalPolicy.Single;

    /// <summary>The any-approver decision rule (approved as soon as anyone approves).</summary>
    public static ApprovalPolicy AnyApprover => ApprovalPolicy.Any;

    /// <summary>The all-approvers decision rule (everyone must approve).</summary>
    public static ApprovalPolicy AllApprovers => ApprovalPolicy.All;

    /// <summary>The majority decision rule (more than half must approve).</summary>
    public static ApprovalPolicy Majority => ApprovalPolicy.Majority;

    /// <summary>The unanimous-consensus decision rule.</summary>
    public static ApprovalPolicy Consensus => ApprovalPolicy.Consensus;

    /// <summary>The first-response decision rule (the first vote decides).</summary>
    public static ApprovalPolicy FirstResponse => ApprovalPolicy.FirstResponse;

    /// <summary>Creates a percentage decision rule.</summary>
    /// <param name="percentage">The required approval percentage (0–100).</param>
    /// <returns>The policy.</returns>
    public static ApprovalPolicy Percentage(double percentage) => ApprovalPolicy.OfPercentage(percentage);

    /// <summary>Creates a weighted-vote decision rule.</summary>
    /// <param name="weightThreshold">The approval weight required to approve.</param>
    /// <returns>The policy.</returns>
    public static ApprovalPolicy Weighted(int weightThreshold) => ApprovalPolicy.OfWeight(weightThreshold);
}
