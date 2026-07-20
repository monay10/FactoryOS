using System.Collections.Concurrent;
using FactoryOS.Identity.Authorization.Caching;
using FactoryOS.Identity.Authorization.Model;

namespace FactoryOS.Identity.Authorization.Services;

/// <summary>
/// The permission catalog and assignment registry: it declares the permission groups and definitions,
/// records role and user assignments, and resolves a principal's effective permissions (role permissions
/// through inheritance, unioned with direct user grants, minus explicit user denials).
/// </summary>
public interface IPermissionService
{
    /// <summary>Declares a permission group (idempotent by key).</summary>
    /// <param name="group">The group to declare.</param>
    void DefineGroup(PermissionGroup group);

    /// <summary>Declares a permission definition (idempotent by key).</summary>
    /// <param name="definition">The definition to declare.</param>
    void Define(PermissionDefinition definition);

    /// <summary>Gets the declared permission groups.</summary>
    /// <returns>The groups.</returns>
    IReadOnlyCollection<PermissionGroup> GetGroups();

    /// <summary>Gets the declared permission definitions.</summary>
    /// <returns>The definitions.</returns>
    IReadOnlyCollection<PermissionDefinition> GetDefinitions();

    /// <summary>Grants a permission to a role.</summary>
    /// <param name="role">The role name.</param>
    /// <param name="permission">The permission key.</param>
    void GrantToRole(string role, string permission);

    /// <summary>Grants a permission directly to a user.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="permission">The permission key.</param>
    void GrantToUser(Guid userId, string permission);

    /// <summary>Explicitly denies a permission to a user, overriding any role grant.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="permission">The permission key.</param>
    void DenyToUser(Guid userId, string permission);

    /// <summary>Resolves the permissions a role grants, including those inherited from parent roles.</summary>
    /// <param name="role">The role name.</param>
    /// <returns>The role's effective permission keys.</returns>
    IReadOnlyCollection<string> ResolveRolePermissions(string role);

    /// <summary>Resolves a user's effective permissions across their roles and direct grants, minus denials.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="roles">The roles the user holds.</param>
    /// <returns>The effective permission keys.</returns>
    IReadOnlyCollection<string> ResolveEffectivePermissions(Guid userId, IEnumerable<string> roles);
}

/// <summary>Default in-memory <see cref="IPermissionService"/> backed by the role service and the cache.</summary>
public sealed class PermissionService : IPermissionService
{
    private readonly ConcurrentDictionary<string, PermissionGroup> _groups = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PermissionDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HashSet<string>> _roleGrants = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _userGrants = new();
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _userDenies = new();

    private readonly IRoleService _roles;
    private readonly IAuthorizationCache _cache;

    /// <summary>Initializes a new instance of the <see cref="PermissionService"/> class.</summary>
    /// <param name="roles">The role service (for inheritance).</param>
    /// <param name="cache">The authorization cache.</param>
    public PermissionService(IRoleService roles, IAuthorizationCache cache)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(cache);
        _roles = roles;
        _cache = cache;
    }

    /// <inheritdoc />
    public void DefineGroup(PermissionGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _groups[group.Key] = group;
    }

    /// <inheritdoc />
    public void Define(PermissionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Key] = definition;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PermissionGroup> GetGroups() => _groups.Values.ToArray();

    /// <inheritdoc />
    public IReadOnlyCollection<PermissionDefinition> GetDefinitions() => _definitions.Values.ToArray();

    /// <inheritdoc />
    public void GrantToRole(string role, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        _roleGrants.GetOrAdd(role, _ => new HashSet<string>(StringComparer.Ordinal)).Add(permission);
        _cache.Clear();
    }

    /// <inheritdoc />
    public void GrantToUser(Guid userId, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        _userGrants.GetOrAdd(userId, _ => new HashSet<string>(StringComparer.Ordinal)).Add(permission);
        _cache.Clear();
    }

    /// <inheritdoc />
    public void DenyToUser(Guid userId, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        _userDenies.GetOrAdd(userId, _ => new HashSet<string>(StringComparer.Ordinal)).Add(permission);
        _cache.Clear();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> ResolveRolePermissions(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        return _cache.GetOrAdd($"perm:role:{role}", () =>
        {
            var permissions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var effectiveRole in _roles.GetEffectiveRoles(role))
            {
                if (_roleGrants.TryGetValue(effectiveRole, out var grants))
                {
                    permissions.UnionWith(grants);
                }
            }

            return permissions.ToArray();
        });
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> ResolveEffectivePermissions(Guid userId, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var roleList = roles.ToArray();
        var key = $"perm:user:{userId:N}:{string.Join(',', roleList.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))}";

        return _cache.GetOrAdd(key, () =>
        {
            var permissions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var role in roleList)
            {
                permissions.UnionWith(ResolveRolePermissions(role));
            }

            if (_userGrants.TryGetValue(userId, out var grants))
            {
                permissions.UnionWith(grants);
            }

            if (_userDenies.TryGetValue(userId, out var denies))
            {
                permissions.ExceptWith(denies);
            }

            return permissions.ToArray();
        });
    }
}
