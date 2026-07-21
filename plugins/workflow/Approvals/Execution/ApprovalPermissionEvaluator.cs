using FactoryOS.Plugins.Workflow.Approvals.Domain;

namespace FactoryOS.Plugins.Workflow.Approvals.Execution;

/// <summary>The identity an approval permission check is made for: a user and the roles and groups they hold.</summary>
/// <param name="UserId">The user id.</param>
/// <param name="Roles">The roles the user holds.</param>
/// <param name="Groups">The groups the user belongs to.</param>
public sealed record ApprovalPrincipal(
    string UserId, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Groups)
{
    /// <summary>Creates a principal for a user with no roles or groups.</summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The principal.</returns>
    public static ApprovalPrincipal ForUser(string userId) => new(userId, [], []);
}

/// <summary>
/// Decides whether a principal may perform an action on an approval. A principal who is the assignee of a
/// pending step in the active stage implicitly holds view, approve, reject and comment rights; other rights
/// come from the definition's permission grants matched to the principal's user id, roles and groups.
/// </summary>
public sealed class ApprovalPermissionEvaluator
{
    private const ApprovalPermission ActiveApproverRights =
        ApprovalPermission.View | ApprovalPermission.Approve |
        ApprovalPermission.Reject | ApprovalPermission.Comment;

    /// <summary>Resolves the effective permissions a principal holds on an approval.</summary>
    /// <param name="definition">The approval definition.</param>
    /// <param name="instance">The approval instance (for the implicit active-approver rights).</param>
    /// <param name="principal">The acting principal.</param>
    /// <returns>The effective permissions.</returns>
    public ApprovalPermission Effective(
        ApprovalDefinition definition, ApprovalInstance instance, ApprovalPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(principal);

        var effective = ApprovalPermission.None;
        var isActiveApprover = instance.ActiveStageSteps.Any(step =>
            step.Status == ApprovalParticipantStatus.Pending
            && string.Equals(step.Assignee, principal.UserId, StringComparison.Ordinal));
        if (isActiveApprover)
        {
            effective |= ActiveApproverRights;
        }

        foreach (var grant in definition.Permissions)
        {
            if (Matches(grant, principal))
            {
                effective |= grant.Permission;
            }
        }

        return effective;
    }

    /// <summary>Determines whether a principal holds every permission in a required set on an approval.</summary>
    /// <param name="definition">The approval definition.</param>
    /// <param name="instance">The approval instance.</param>
    /// <param name="principal">The acting principal.</param>
    /// <param name="required">The required permission(s).</param>
    /// <returns><see langword="true"/> when the principal is allowed.</returns>
    public bool HasPermission(
        ApprovalDefinition definition,
        ApprovalInstance instance,
        ApprovalPrincipal principal,
        ApprovalPermission required) =>
        (Effective(definition, instance, principal) & required) == required;

    private static bool Matches(ApprovalPermissionGrant grant, ApprovalPrincipal principal) => grant.Kind switch
    {
        ApprovalPrincipalKind.User => string.Equals(grant.Principal, principal.UserId, StringComparison.Ordinal),
        ApprovalPrincipalKind.Role => principal.Roles.Contains(grant.Principal, StringComparer.Ordinal),
        ApprovalPrincipalKind.Group => principal.Groups.Contains(grant.Principal, StringComparer.Ordinal),
        _ => false,
    };
}
