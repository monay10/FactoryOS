namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>
/// The answer, and why.
/// <para>
/// A decision is never a bare boolean. Every denial names the reason, the rule or policy that produced it, and
/// the permission that was asked for — because the question that follows a denial is always "why?", and an
/// authorization layer that cannot answer it is one that gets worked around instead of fixed. The same applies
/// to a grant: knowing <i>which</i> rule opened a door is how an over-broad grant is ever found.
/// </para>
/// </summary>
/// <param name="Effect">Whether the request is permitted.</param>
/// <param name="Reason">Why.</param>
/// <param name="Permission">The permission that was asked for.</param>
/// <param name="Description">A sentence a human on the other end of the denial can act on.</param>
/// <param name="PolicyKey">The policy that decided, when a policy did.</param>
/// <param name="RuleKey">The rule that decided, when a rule did.</param>
/// <param name="FailedConstraint">The constraint that was not satisfied, when one was not.</param>
/// <param name="Correlation">The identifiers tying the decision to the request that asked for it.</param>
public sealed record SecurityDecision(
    SecurityEffect Effect,
    SecurityDecisionReason Reason,
    string Permission,
    string Description,
    string? PolicyKey = null,
    string? RuleKey = null,
    string? FailedConstraint = null,
    SecurityCorrelation? Correlation = null)
{
    /// <summary>Gets a value indicating whether the request is permitted.</summary>
    public bool IsAllowed => Effect == SecurityEffect.Allow;

    /// <summary>Gets a value indicating whether the request is refused.</summary>
    public bool IsDenied => Effect == SecurityEffect.Deny;

    /// <summary>Builds an allow.</summary>
    /// <param name="reason">Why it was allowed.</param>
    /// <param name="permission">The permission that was asked for.</param>
    /// <param name="description">A sentence describing the grant.</param>
    /// <param name="policyKey">The deciding policy.</param>
    /// <param name="ruleKey">The deciding rule.</param>
    /// <param name="correlation">The request's correlation.</param>
    /// <returns>The decision.</returns>
    public static SecurityDecision Allow(
        SecurityDecisionReason reason,
        string permission,
        string description,
        string? policyKey = null,
        string? ruleKey = null,
        SecurityCorrelation? correlation = null) =>
        new(SecurityEffect.Allow, reason, permission, description, policyKey, ruleKey, null, correlation);

    /// <summary>Builds a deny.</summary>
    /// <param name="reason">Why it was refused.</param>
    /// <param name="permission">The permission that was asked for.</param>
    /// <param name="description">A sentence describing the refusal.</param>
    /// <param name="policyKey">The deciding policy.</param>
    /// <param name="ruleKey">The deciding rule.</param>
    /// <param name="failedConstraint">The constraint that was not satisfied.</param>
    /// <param name="correlation">The request's correlation.</param>
    /// <returns>The decision.</returns>
    public static SecurityDecision Deny(
        SecurityDecisionReason reason,
        string permission,
        string description,
        string? policyKey = null,
        string? ruleKey = null,
        string? failedConstraint = null,
        SecurityCorrelation? correlation = null) =>
        new(
            SecurityEffect.Deny, reason, permission, description, policyKey, ruleKey, failedConstraint,
            correlation);

    /// <inheritdoc />
    public override string ToString() => $"{Effect} {Permission} ({Reason}): {Description}";
}
