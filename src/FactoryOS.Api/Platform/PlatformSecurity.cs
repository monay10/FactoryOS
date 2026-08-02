using System.Collections.Generic;
using System.Linq;
using FactoryOS.Plugins.Runtime.Domain;
using SecurityEngine = FactoryOS.Plugins.Workflow.Security.Execution.SecurityEngine;

namespace FactoryOS.Api.Platform;

// The read projection and the permission vocabulary behind the security-grants management surface. Managing who may
// do what is sensitive, so the endpoints live on the authenticated /platform management surface (not the open
// observability read) and require the caller to hold a security permission. The projection is a pure function over
// the composed security engine so it can be tested without the HTTP pipeline; the mutations (grant, revoke) are thin
// calls the endpoint makes directly, attributed to the caller.

/// <summary>The permissions granted directly to a subject in a tenant.</summary>
/// <param name="Tenant">The tenant the grants belong to.</param>
/// <param name="Subject">The subject the grants are held by.</param>
/// <param name="Grants">The directly granted permission strings, ordered.</param>
internal sealed record SecurityGrantsView(string Tenant, string Subject, IReadOnlyList<string> Grants);

/// <summary>One subject in a tenant's grants roster, with the permissions it holds directly.</summary>
/// <param name="Subject">The subject.</param>
/// <param name="Grants">The subject's directly granted permission strings, ordered.</param>
internal sealed record SecuritySubjectGrantsView(string Subject, IReadOnlyList<string> Grants);

/// <summary>Every subject that holds a direct grant in a tenant, so the roster is browsable without guessing.</summary>
/// <param name="Tenant">The tenant the roster belongs to.</param>
/// <param name="Subjects">The subjects with grants, ordered by subject.</param>
internal sealed record SecurityRosterView(string Tenant, IReadOnlyList<SecuritySubjectGrantsView> Subjects);

/// <summary>Pure helpers and the permission vocabulary behind the <c>/platform/security</c> management surface.</summary>
internal static class PlatformSecurity
{
    /// <summary>The permission a caller must hold to read a subject's grants.</summary>
    public static readonly PluginPermission ReadGrants = PluginPermission.Of("security", "read");

    /// <summary>The permission a caller must hold to grant or revoke a subject's permissions.</summary>
    public static readonly PluginPermission WriteGrants = PluginPermission.Of("security", "grant");

    /// <summary>
    /// Projects the permissions granted directly to a subject in a tenant, ordered for a stable read. These are the
    /// subject's own grants — the permissions it inherits through its roles are not included.
    /// </summary>
    /// <param name="security">The composed security engine.</param>
    /// <param name="tenant">The tenant whose grants are read.</param>
    /// <param name="subject">The subject whose grants are read.</param>
    /// <returns>The subject's grants.</returns>
    public static SecurityGrantsView Grants(SecurityEngine security, string tenant, string subject)
    {
        ArgumentNullException.ThrowIfNull(security);

        var grants = security.GrantsFor(tenant, subject)
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToArray();

        return new SecurityGrantsView(tenant, subject, grants);
    }

    /// <summary>
    /// Projects every subject that holds a direct grant in a tenant, grouped by subject and ordered, so an operator
    /// can browse who has what rather than having to know a subject to look one up.
    /// </summary>
    /// <param name="security">The composed security engine.</param>
    /// <param name="tenant">The tenant whose roster is read.</param>
    /// <returns>The tenant's grants roster.</returns>
    public static SecurityRosterView Roster(SecurityEngine security, string tenant)
    {
        ArgumentNullException.ThrowIfNull(security);

        var subjects = security.GrantsIn(tenant)
            .GroupBy(entry => entry.Subject, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SecuritySubjectGrantsView(
                group.Key,
                [.. group.Select(entry => entry.Permission).OrderBy(permission => permission, StringComparer.Ordinal)]))
            .ToArray();

        return new SecurityRosterView(tenant, subjects);
    }
}
