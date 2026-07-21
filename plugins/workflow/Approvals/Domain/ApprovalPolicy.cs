namespace FactoryOS.Plugins.Workflow.Approvals.Domain;

/// <summary>A one-based approval level; the position of a stage in a sequential approval.</summary>
/// <param name="Value">The level number (1 or greater).</param>
public readonly record struct ApprovalLevel(int Value)
{
    /// <summary>The first level.</summary>
    public static readonly ApprovalLevel First = new(1);

    /// <summary>Returns the next level.</summary>
    /// <returns>A level one greater than this one.</returns>
    public ApprovalLevel Next() => new(Value + 1);
}

/// <summary>
/// The decision rule a stage uses to turn its participants' votes into an outcome. The <see cref="Kind"/>
/// selects the rule; <see cref="Percentage"/> and <see cref="WeightThreshold"/> parameterize the percentage
/// and weighted-vote rules respectively.
/// </summary>
public sealed record ApprovalPolicy
{
    private ApprovalPolicy(ApprovalPolicyKind kind, double? percentage, int? weightThreshold)
    {
        Kind = kind;
        Percentage = percentage;
        WeightThreshold = weightThreshold;
    }

    /// <summary>Gets the decision rule.</summary>
    public ApprovalPolicyKind Kind { get; }

    /// <summary>Gets the required approval percentage (0–100) for the percentage rule.</summary>
    public double? Percentage { get; }

    /// <summary>Gets the required approval weight for the weighted-vote rule.</summary>
    public int? WeightThreshold { get; }

    /// <summary>A single-approver rule.</summary>
    public static ApprovalPolicy Single { get; } = new(ApprovalPolicyKind.Single, null, null);

    /// <summary>An any-approver rule.</summary>
    public static ApprovalPolicy Any { get; } = new(ApprovalPolicyKind.AnyApprover, null, null);

    /// <summary>An all-approvers rule.</summary>
    public static ApprovalPolicy All { get; } = new(ApprovalPolicyKind.AllApprovers, null, null);

    /// <summary>A majority rule.</summary>
    public static ApprovalPolicy Majority { get; } = new(ApprovalPolicyKind.Majority, null, null);

    /// <summary>A unanimous-consensus rule.</summary>
    public static ApprovalPolicy Consensus { get; } = new(ApprovalPolicyKind.Consensus, null, null);

    /// <summary>A first-response rule.</summary>
    public static ApprovalPolicy FirstResponse { get; } = new(ApprovalPolicyKind.FirstResponse, null, null);

    /// <summary>Creates a percentage rule.</summary>
    /// <param name="percentage">The required approval percentage (0–100).</param>
    /// <returns>The policy.</returns>
    public static ApprovalPolicy OfPercentage(double percentage)
    {
        if (percentage is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), percentage, "Percentage must be in (0, 100].");
        }

        return new ApprovalPolicy(ApprovalPolicyKind.Percentage, percentage, null);
    }

    /// <summary>Creates a weighted-vote rule.</summary>
    /// <param name="weightThreshold">The approval weight required to approve.</param>
    /// <returns>The policy.</returns>
    public static ApprovalPolicy OfWeight(int weightThreshold)
    {
        if (weightThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weightThreshold), weightThreshold, "The weight threshold must be positive.");
        }

        return new ApprovalPolicy(ApprovalPolicyKind.WeightedVote, null, weightThreshold);
    }
}
