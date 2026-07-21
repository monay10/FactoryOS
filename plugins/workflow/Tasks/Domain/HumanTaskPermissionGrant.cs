namespace FactoryOS.Plugins.Workflow.Tasks.Domain;

/// <summary>The kind of subject a <see cref="HumanTaskPermissionGrant"/> targets.</summary>
public enum HumanTaskPrincipalKind
{
    /// <summary>A specific user.</summary>
    User = 0,

    /// <summary>A role.</summary>
    Role = 1,

    /// <summary>A group.</summary>
    Group = 2,
}

/// <summary>
/// Grants a principal a set of actions on a human task. Interpreted alongside the implicit rights the current
/// assignee always holds (read, write, complete, reject).
/// </summary>
/// <param name="Kind">How to interpret the principal.</param>
/// <param name="Principal">The user id, role or group name.</param>
/// <param name="Permission">The actions granted.</param>
public sealed record HumanTaskPermissionGrant(
    HumanTaskPrincipalKind Kind, string Principal, HumanTaskPermission Permission);
