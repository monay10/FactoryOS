using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Persistence;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>A rule that spoke about a request, and the policy it came from.</summary>
/// <param name="Policy">The policy.</param>
/// <param name="Rule">The rule.</param>
public sealed record PolicyMatch(SecurityPolicy Policy, SecurityRule Rule);

/// <summary>
/// What the policies had to say about a request.
/// </summary>
/// <param name="Deny">The refusing rule, when one matched with its constraints satisfied.</param>
/// <param name="Allow">The granting rule, when one matched with its constraints satisfied.</param>
/// <param name="Blocked">A granting rule that matched but whose constraint did not hold.</param>
/// <param name="BlockedBy">The constraint that did not hold.</param>
/// <param name="AnyRuleSpeaks">Whether any rule at all addressed the permission being asked for.</param>
public sealed record PolicyEvaluation(
    PolicyMatch? Deny,
    PolicyMatch? Allow,
    PolicyMatch? Blocked,
    SecurityConstraint? BlockedBy,
    bool AnyRuleSpeaks)
{
    /// <summary>An evaluation in which no policy said anything.</summary>
    public static PolicyEvaluation Silent { get; } = new(null, null, null, null, false);
}

/// <summary>
/// Runs a tenant's policies over a request.
/// <para>
/// The combining algorithm is fixed and deliberately dull: <b>an explicit deny always wins</b>, then a grant,
/// then nothing. Priority orders rules of the same effect against each other and never lets an allow outrank a
/// deny. This is the one place in the engine where being clever would be a security bug — every notorious
/// authorization failure is some variation on "the allow was evaluated after the deny".
/// </para>
/// </summary>
public sealed class PolicyEvaluator
{
    private readonly ISecurityRepository _repository;

    /// <summary>Initializes a new instance of the <see cref="PolicyEvaluator"/> class.</summary>
    /// <param name="repository">The policy registry.</param>
    public PolicyEvaluator(ISecurityRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>Evaluates the policies that apply to a request.</summary>
    /// <param name="context">The request.</param>
    /// <returns>What the policies had to say.</returns>
    public PolicyEvaluation Evaluate(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requested = context.RequestedPermission;
        var policies = _repository.PoliciesFor(context.Scope.Tenant);

        PolicyMatch? allow = null;
        PolicyMatch? blocked = null;
        SecurityConstraint? blockedBy = null;
        var anyRuleSpeaks = false;

        foreach (var policy in policies)
        {
            foreach (var rule in policy.Rules.OrderByDescending(rule => rule.Priority))
            {
                if (!rule.Pattern.Grants(requested))
                {
                    continue;
                }

                // A rule that addresses the permission counts as "somebody thought about this", even when it
                // is aimed at a different principal. That is what separates "you lack the permission" from
                // "nothing in this platform grants it to anyone".
                anyRuleSpeaks = true;

                if (!rule.Subject.IsSatisfiedBy(context.Principal))
                {
                    continue;
                }

                if (rule.FirstUnsatisfied(context) is { } unsatisfied)
                {
                    if (rule.Effect == SecurityEffect.Allow && blocked is null)
                    {
                        blocked = new PolicyMatch(policy, rule);
                        blockedBy = unsatisfied;
                    }

                    continue;
                }

                if (rule.Effect == SecurityEffect.Deny)
                {
                    // The first satisfied deny is enough; nothing later can overturn it, so there is no
                    // reason to keep looking for a second one.
                    return new PolicyEvaluation(
                        new PolicyMatch(policy, rule), allow, blocked, blockedBy, true);
                }

                allow ??= new PolicyMatch(policy, rule);
            }
        }

        // Reaching here means no deny was satisfied — a satisfied deny returns immediately above.
        return allow is null && blocked is null && !anyRuleSpeaks
            ? PolicyEvaluation.Silent
            : new PolicyEvaluation(null, allow, blocked, blockedBy, anyRuleSpeaks);
    }
}
