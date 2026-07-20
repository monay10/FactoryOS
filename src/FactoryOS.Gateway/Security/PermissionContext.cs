namespace FactoryOS.Gateway.Security;

/// <summary>
/// The mutable, request-scoped backing store for <see cref="IPermissionContext"/>. Only the permission-resolution
/// middleware writes to it, at the start of the request; endpoints only read. Registered as a scoped service so
/// each request gets its own isolated instance and permissions can never bleed across requests. Until the
/// middleware sets a permission set the context is unrestricted.
/// </summary>
internal sealed class PermissionContext : IPermissionContext
{
    private HashSet<string>? _permissions;

    public bool Unrestricted => _permissions is null;

    public IReadOnlyCollection<string> Permissions => _permissions is null ? [] : _permissions;

    public bool Holds(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        if (_permissions is null)
        {
            return true;
        }

        // Fast path for an exact grant, then wildcard-aware matching (e.g. a held "energy.*" grants "energy.view").
        return _permissions.Contains(permission)
            || _permissions.Any(grant => PermissionMatch.Grants(grant, permission));
    }

    /// <summary>Records the permission set resolved for the request. Invoked once by the middleware.</summary>
    /// <param name="permissions">The resolved permissions (may be empty, meaning "explicitly none").</param>
    internal void Set(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
    }
}
