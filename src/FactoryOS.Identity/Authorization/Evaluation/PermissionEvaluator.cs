using FactoryOS.Identity.Authorization.Configuration;

namespace FactoryOS.Identity.Authorization.Evaluation;

/// <summary>Evaluates a set of granted permission keys against a required permission key.</summary>
public interface IPermissionEvaluator
{
    /// <summary>Determines whether any granted permission covers the required one.</summary>
    /// <param name="grantedPermissions">The permission keys the principal holds.</param>
    /// <param name="requiredPermission">The permission key being checked.</param>
    /// <returns><see langword="true"/> when the requirement is satisfied.</returns>
    bool Evaluate(IEnumerable<string> grantedPermissions, string requiredPermission);

    /// <summary>Determines whether a single granted permission covers the required one.</summary>
    /// <param name="grantedPermission">The permission key held.</param>
    /// <param name="requiredPermission">The permission key being checked.</param>
    /// <returns><see langword="true"/> when the grant covers the requirement.</returns>
    bool Grants(string grantedPermission, string requiredPermission);
}

/// <summary>
/// Default <see cref="IPermissionEvaluator"/> supporting both wildcard and hierarchical grants over
/// dot-separated permission keys:
/// <list type="bullet">
/// <item><description><c>*</c> alone grants everything (super-admin).</description></item>
/// <item><description>A trailing <c>*</c> segment grants every descendant, e.g. <c>energy.*</c> grants
/// <c>energy.read</c> and <c>energy.read.detail</c>.</description></item>
/// <item><description>A shorter key grants its descendants, e.g. <c>energy</c> grants <c>energy.read</c>
/// (hierarchical). A more specific key never grants a broader one.</description></item>
/// </list>
/// </summary>
public sealed class PermissionEvaluator : IPermissionEvaluator
{
    /// <inheritdoc />
    public bool Evaluate(IEnumerable<string> grantedPermissions, string requiredPermission)
    {
        ArgumentNullException.ThrowIfNull(grantedPermissions);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredPermission);

        return grantedPermissions.Any(granted => Grants(granted, requiredPermission));
    }

    /// <inheritdoc />
    public bool Grants(string grantedPermission, string requiredPermission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredPermission);

        if (string.IsNullOrWhiteSpace(grantedPermission))
        {
            return false;
        }

        var granted = Normalize(grantedPermission);
        if (granted == AuthorizationConstants.Wildcard)
        {
            return true;
        }

        var grantedSegments = granted.Split(AuthorizationConstants.HierarchySeparator);
        var requiredSegments = Normalize(requiredPermission).Split(AuthorizationConstants.HierarchySeparator);

        for (var i = 0; i < grantedSegments.Length; i++)
        {
            // The grant is more specific than the requirement — it cannot cover it.
            if (i >= requiredSegments.Length)
            {
                return false;
            }

            // A wildcard segment consumes the remainder of the requirement.
            if (grantedSegments[i] == AuthorizationConstants.Wildcard)
            {
                return true;
            }

            if (!string.Equals(grantedSegments[i], requiredSegments[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        // Every granted segment matched a requirement segment: the grant equals or is a prefix of
        // (i.e. hierarchically covers) the requirement.
        return true;
    }

    private static string Normalize(string permission) => permission.Trim().ToLowerInvariant();
}
