using FactoryOS.Gateway.Ui;

namespace FactoryOS.Gateway.Security;

/// <summary>
/// Filters a <see cref="NavCatalog"/> to the screens the current request is allowed to see. A screen with no
/// declared permission is always kept; a screen that declares one is kept only when the request holds it. Sections
/// left empty by filtering are dropped. An unrestricted request passes through unchanged — RBAC narrows the surface
/// only when a permission set was resolved.
/// </summary>
public static class NavigationPermissionFilter
{
    /// <summary>Returns a navigation catalog containing only the screens the permissions allow.</summary>
    /// <param name="nav">The full navigation catalog.</param>
    /// <param name="permissions">The request's permission context.</param>
    /// <returns>The filtered catalog (the same instance when the request is unrestricted).</returns>
    public static NavCatalog Apply(NavCatalog nav, IPermissionContext permissions)
    {
        ArgumentNullException.ThrowIfNull(nav);
        ArgumentNullException.ThrowIfNull(permissions);

        if (permissions.Unrestricted)
        {
            return nav;
        }

        var sections = nav.Sections
            .Select(section => new NavSection(
                section.Section,
                section.Items.Where(item => IsAllowed(item, permissions)).ToArray()))
            .Where(section => section.Items.Count > 0)
            .ToArray();

        return new NavCatalog(sections);
    }

    private static bool IsAllowed(NavItem item, IPermissionContext permissions) =>
        string.IsNullOrWhiteSpace(item.RequiredPermission) || permissions.Holds(item.RequiredPermission);
}
