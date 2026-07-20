namespace FactoryOS.Gateway.Security;

/// <summary>
/// The permissions the current request holds, resolved once at the edge. The shell uses it to filter navigation:
/// a screen that declares a <c>requiredPermission</c> is shown only when the caller holds it. When no identity is
/// presented the context is <see cref="Unrestricted"/> — with no basis to restrict, everything is visible — so
/// RBAC is additive: it narrows the surface only when a permission set is actually supplied.
/// </summary>
public interface IPermissionContext
{
    /// <summary>Gets a value indicating whether the request is unrestricted (no permission set was resolved).</summary>
    bool Unrestricted { get; }

    /// <summary>Gets the permissions resolved for the request (empty when <see cref="Unrestricted"/>).</summary>
    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>Determines whether the request holds a permission (always true when unrestricted).</summary>
    /// <param name="permission">The permission key to check.</param>
    /// <returns><see langword="true"/> when the request holds the permission.</returns>
    bool Holds(string permission);
}
