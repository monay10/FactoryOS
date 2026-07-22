using FactoryOS.Plugins.Workflow.Security.Domain;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>
/// Decides. Given everything about a request, it produces a <see cref="SecurityDecision"/> that says yes or no
/// and says why.
/// <para>
/// The order of the checks is the design, and it is not negotiable:
/// </para>
/// <list type="number">
///   <item><description><b>Authenticated?</b> An anonymous principal holds nothing.</description></item>
///   <item><description><b>Same tenant?</b> Checked structurally, before any policy runs, and no rule can
///   grant around it. Per the Constitution there is no code path that reads or writes across tenants — so this
///   is an invariant, not a policy.</description></item>
///   <item><description><b>Any deny?</b> An explicit refusal ends it.</description></item>
///   <item><description><b>Any grant?</b> From a rule, or from a permission the principal holds.</description></item>
///   <item><description><b>Otherwise, no.</b> Nothing is permitted by default.</description></item>
/// </list>
/// <para>
/// One consequence is worth stating plainly: <b>a constraint narrows the rule it is attached to, not the
/// permission.</b> A principal holding <c>audit.export</c> outright is not stopped by a time window written on
/// some other rule. To bind everybody regardless of what they hold, write a deny — that is what denies are
/// for, and they always win.
/// </para>
/// </summary>
public sealed class AuthorizationEngine
{
    private readonly PermissionEvaluator _permissions;
    private readonly PolicyEvaluator _policies;

    /// <summary>Initializes a new instance of the <see cref="AuthorizationEngine"/> class.</summary>
    /// <param name="permissions">The permission evaluator.</param>
    /// <param name="policies">The policy evaluator.</param>
    public AuthorizationEngine(PermissionEvaluator permissions, PolicyEvaluator policies)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(policies);
        _permissions = permissions;
        _policies = policies;
    }

    /// <summary>Decides a request.</summary>
    /// <param name="context">Everything the decision is made from.</param>
    /// <returns>The decision, carrying why it went the way it did.</returns>
    public SecurityDecision Authorize(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requested = context.RequestedPermission;
        var permission = requested.Value;
        var correlation = context.Correlation;

        if (!context.Principal.IsAuthenticated)
        {
            return SecurityDecision.Deny(
                SecurityDecisionReason.NotAuthenticated,
                permission,
                "The request presented no authenticated principal.",
                correlation: correlation);
        }

        if (!context.IsSameTenant)
        {
            return SecurityDecision.Deny(
                SecurityDecisionReason.TenantMismatch,
                permission,
                $"The principal belongs to '{context.Principal.Tenant}' and the request names "
                + $"'{context.Scope.Tenant}'.",
                correlation: correlation);
        }

        var evaluation = _policies.Evaluate(context);

        if (evaluation.Deny is { } deny)
        {
            return SecurityDecision.Deny(
                SecurityDecisionReason.DeniedByRule,
                permission,
                deny.Rule.Description.Length > 0
                    ? deny.Rule.Description
                    : $"Rule '{deny.Rule.Key}' refuses {permission}.",
                deny.Policy.Key,
                deny.Rule.Key,
                correlation: correlation);
        }

        if (evaluation.Allow is { } allow)
        {
            return SecurityDecision.Allow(
                SecurityDecisionReason.GrantedByRule,
                permission,
                allow.Rule.Description.Length > 0
                    ? allow.Rule.Description
                    : $"Rule '{allow.Rule.Key}' grants {permission}.",
                allow.Policy.Key,
                allow.Rule.Key,
                correlation);
        }

        if (_permissions.CoveringGrant(context.Principal, requested) is { } grant)
        {
            return SecurityDecision.Allow(
                SecurityDecisionReason.GrantedByPermission,
                permission,
                $"The principal holds '{grant.Value}'.",
                correlation: correlation);
        }

        // A grant that was within reach but for one condition is the most useful denial there is: it tells
        // somebody what to change instead of leaving them to guess which of their permissions is missing.
        if (evaluation.Blocked is { } blocked && evaluation.BlockedBy is { } constraint)
        {
            return SecurityDecision.Deny(
                SecurityDecisionReason.ConstraintNotSatisfied,
                permission,
                $"Rule '{blocked.Rule.Key}' would grant {permission}, but requires {constraint.Describe()}.",
                blocked.Policy.Key,
                blocked.Rule.Key,
                constraint.Key,
                correlation);
        }

        return evaluation.AnyRuleSpeaks
            ? SecurityDecision.Deny(
                SecurityDecisionReason.MissingPermission,
                permission,
                $"The principal holds nothing covering {permission}.",
                correlation: correlation)
            : SecurityDecision.Deny(
                SecurityDecisionReason.NoMatchingRule,
                permission,
                $"No policy grants {permission} to anyone, and the principal holds nothing covering it.",
                correlation: correlation);
    }
}
