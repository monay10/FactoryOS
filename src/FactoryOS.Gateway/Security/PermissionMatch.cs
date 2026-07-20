namespace FactoryOS.Gateway.Security;

/// <summary>
/// Matches a held permission grant against a required permission, honoring the same <c>resource.action</c>
/// wildcard convention the FactoryOS Identity layer issues: <c>*</c> grants everything, <c>resource.*</c> grants
/// every action on a resource, and an exact <c>resource.action</c> grants only itself. The gateway matches on this
/// convention alone — it never references the Identity permission type.
/// </summary>
public static class PermissionMatch
{
    /// <summary>The wildcard token that matches any resource or action.</summary>
    public const string Wildcard = "*";

    /// <summary>Determines whether a held grant covers a required permission.</summary>
    /// <param name="grant">The permission the caller holds (may contain wildcards).</param>
    /// <param name="required">The concrete permission a screen requires.</param>
    /// <returns><see langword="true"/> when <paramref name="grant"/> grants <paramref name="required"/>.</returns>
    public static bool Grants(string grant, string required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grant);
        ArgumentException.ThrowIfNullOrWhiteSpace(required);

        if (grant == Wildcard || string.Equals(grant, required, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // A "resource.*" grant covers every action on that resource.
        var star = grant.IndexOf(".*", StringComparison.Ordinal);
        if (star > 0 && star == grant.Length - 2)
        {
            var resource = grant.AsSpan(0, star);
            var dot = required.IndexOf('.');
            return dot == resource.Length
                && required.AsSpan(0, dot).Equals(resource, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
