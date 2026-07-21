using FactoryOS.Plugins.Forms.Engine.Domain;

namespace FactoryOS.Plugins.Forms.Engine.Execution;

/// <summary>The identity a form access check is made for: a user and the roles and groups they hold.</summary>
/// <param name="UserId">The user id.</param>
/// <param name="Roles">The roles the user holds.</param>
/// <param name="Groups">The groups the user belongs to.</param>
public sealed record FormPrincipal(
    string UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Groups)
{
    /// <summary>Creates a principal for a user with no roles or groups.</summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The principal.</returns>
    public static FormPrincipal ForUser(string userId) => new(userId, [], []);
}

/// <summary>
/// Decides whether a principal may perform an action on a form. Access is ordered
/// (<see cref="FormAccess.View"/> &lt; <see cref="FormAccess.Edit"/> &lt; <see cref="FormAccess.Submit"/> &lt;
/// <see cref="FormAccess.Approve"/>); a grant confers that level and every lower one. A form with no
/// permissions declared is open — every principal may act — so simple forms need no configuration.
/// </summary>
public sealed class FormPermissionEvaluator
{
    /// <summary>Determines whether a principal has at least the required access to a form.</summary>
    /// <param name="definition">The form definition.</param>
    /// <param name="principal">The acting principal.</param>
    /// <param name="required">The access being checked.</param>
    /// <returns><see langword="true"/> when the principal is allowed.</returns>
    public bool HasAccess(FormDefinition definition, FormPrincipal principal, FormAccess required)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(principal);

        if (definition.Permissions.Count == 0)
        {
            return true;
        }

        foreach (var permission in definition.Permissions)
        {
            if (Matches(permission, principal) && permission.Access >= required)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(FormPermission permission, FormPrincipal principal) => permission.Kind switch
    {
        FormPrincipalKind.User => string.Equals(permission.Principal, principal.UserId, StringComparison.Ordinal),
        FormPrincipalKind.Role => principal.Roles.Contains(permission.Principal, StringComparer.Ordinal),
        FormPrincipalKind.Group => principal.Groups.Contains(permission.Principal, StringComparer.Ordinal),
        _ => false,
    };
}
