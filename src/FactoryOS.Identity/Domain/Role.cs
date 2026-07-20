using FactoryOS.Domain.Primitives;
using FactoryOS.Identity.Authorization;

namespace FactoryOS.Identity.Domain;

/// <summary>A named set of permissions within a tenant, assignable to users.</summary>
public sealed class Role : AggregateRoot<Guid>
{
    private readonly HashSet<Permission> _permissions = [];

    private Role(Guid id, Guid tenantId, string name)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
    }

    private Role() => Name = string.Empty;

    /// <summary>Gets the owning tenant identifier.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the role name (e.g. <c>Operator</c>, <c>Administrator</c>).</summary>
    public string Name { get; private set; }

    /// <summary>Gets the permissions granted by this role.</summary>
    public IReadOnlyCollection<Permission> Permissions => _permissions;

    /// <summary>Creates a new role.</summary>
    /// <param name="id">The role identifier.</param>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="name">The role name.</param>
    /// <returns>The new role.</returns>
    public static Role Create(Guid id, Guid tenantId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Role(id, tenantId, name);
    }

    /// <summary>Grants a permission to the role.</summary>
    /// <param name="permission">The permission to grant.</param>
    public void Grant(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        _permissions.Add(permission);
    }

    /// <summary>Revokes a permission from the role.</summary>
    /// <param name="permission">The permission to revoke.</param>
    /// <returns><see langword="true"/> when the permission was present and removed.</returns>
    public bool Revoke(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        return _permissions.Remove(permission);
    }

    /// <summary>Determines whether the role grants the requested permission (honoring wildcards).</summary>
    /// <param name="requested">The concrete permission being checked.</param>
    /// <returns><see langword="true"/> when any granted permission covers <paramref name="requested"/>.</returns>
    public bool Grants(Permission requested)
    {
        ArgumentNullException.ThrowIfNull(requested);
        return _permissions.Any(permission => permission.Grants(requested));
    }
}
