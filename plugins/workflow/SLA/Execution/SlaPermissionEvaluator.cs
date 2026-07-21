using FactoryOS.Plugins.Workflow.SLA.Domain;

namespace FactoryOS.Plugins.Workflow.SLA.Execution;

/// <summary>
/// Decides what a principal may do with an SLA. Rights come from the definition's grants, matched by user id,
/// <c>role:x</c> or <c>group:x</c>; the principal who started the SLA implicitly holds <see cref="SlaPermission.View"/>
/// over it. Grants accumulate, so several rungs of a hierarchy can each add rights.
/// </summary>
public sealed class SlaPermissionEvaluator
{
    /// <summary>Computes the rights a principal holds over an SLA.</summary>
    /// <param name="definition">The SLA definition supplying the grants.</param>
    /// <param name="sla">The SLA instance.</param>
    /// <param name="principal">The principal (a user id, <c>role:x</c> or <c>group:x</c>).</param>
    /// <param name="startedBy">Who started the SLA, if known.</param>
    /// <returns>The accumulated rights.</returns>
    public SlaPermission Evaluate(
        SlaDefinition definition, SlaInstance sla, string principal, string? startedBy = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(sla);
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);

        var permission = SlaPermission.None;
        if (string.Equals(principal, startedBy, StringComparison.Ordinal))
        {
            permission |= SlaPermission.View;
        }

        foreach (var grant in definition.Grants)
        {
            if (string.Equals(grant.Principal, principal, StringComparison.Ordinal))
            {
                permission |= grant.Permission;
            }
        }

        return permission;
    }

    /// <summary>Gets a value indicating whether a principal holds a right over an SLA.</summary>
    /// <param name="definition">The SLA definition.</param>
    /// <param name="sla">The SLA instance.</param>
    /// <param name="principal">The principal.</param>
    /// <param name="permission">The right to test.</param>
    /// <param name="startedBy">Who started the SLA, if known.</param>
    /// <returns><see langword="true"/> when the principal holds the right.</returns>
    public bool Allows(
        SlaDefinition definition,
        SlaInstance sla,
        string principal,
        SlaPermission permission,
        string? startedBy = null) =>
        Evaluate(definition, sla, principal, startedBy).HasFlag(permission);
}
