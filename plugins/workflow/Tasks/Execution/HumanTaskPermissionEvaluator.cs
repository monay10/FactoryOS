using FactoryOS.Plugins.Workflow.Tasks.Domain;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>The identity a task permission check is made for: a user and the roles and groups they hold.</summary>
/// <param name="UserId">The user id.</param>
/// <param name="Roles">The roles the user holds.</param>
/// <param name="Groups">The groups the user belongs to.</param>
public sealed record HumanTaskPrincipal(
    string UserId, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Groups)
{
    /// <summary>Creates a principal for a user with no roles or groups.</summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The principal.</returns>
    public static HumanTaskPrincipal ForUser(string userId) => new(userId, [], []);
}

/// <summary>
/// Decides whether a principal may perform an action on a human task. The current assignee always holds an
/// implicit set of rights (read, write, complete, reject); other rights come from the definition's permission
/// grants matched to the principal's user id, roles and groups.
/// </summary>
public sealed class HumanTaskPermissionEvaluator
{
    private const HumanTaskPermission AssigneeRights =
        HumanTaskPermission.Read | HumanTaskPermission.Write |
        HumanTaskPermission.Complete | HumanTaskPermission.Reject;

    /// <summary>Resolves the effective permissions a principal holds on a task.</summary>
    /// <param name="definition">The task definition.</param>
    /// <param name="instance">The task instance (for the implicit assignee rights).</param>
    /// <param name="principal">The acting principal.</param>
    /// <returns>The effective permissions.</returns>
    public HumanTaskPermission Effective(
        HumanTaskDefinition definition, HumanTaskInstance instance, HumanTaskPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(principal);

        var effective = HumanTaskPermission.None;
        if (string.Equals(instance.Assignee, principal.UserId, StringComparison.Ordinal))
        {
            effective |= AssigneeRights;
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

    /// <summary>Determines whether a principal holds every permission in a required set on a task.</summary>
    /// <param name="definition">The task definition.</param>
    /// <param name="instance">The task instance.</param>
    /// <param name="principal">The acting principal.</param>
    /// <param name="required">The required permission(s).</param>
    /// <returns><see langword="true"/> when the principal is allowed.</returns>
    public bool HasPermission(
        HumanTaskDefinition definition,
        HumanTaskInstance instance,
        HumanTaskPrincipal principal,
        HumanTaskPermission required) =>
        (Effective(definition, instance, principal) & required) == required;

    private static bool Matches(HumanTaskPermissionGrant grant, HumanTaskPrincipal principal) => grant.Kind switch
    {
        HumanTaskPrincipalKind.User => string.Equals(grant.Principal, principal.UserId, StringComparison.Ordinal),
        HumanTaskPrincipalKind.Role => principal.Roles.Contains(grant.Principal, StringComparer.Ordinal),
        HumanTaskPrincipalKind.Group => principal.Groups.Contains(grant.Principal, StringComparer.Ordinal),
        _ => false,
    };
}
