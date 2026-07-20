using System.Collections.Concurrent;
using FactoryOS.Identity.Authorization.Caching;
using FactoryOS.Identity.Authorization.Configuration;
using Microsoft.Extensions.Options;

namespace FactoryOS.Identity.Authorization.Services;

/// <summary>Registers roles and resolves role inheritance (a role's transitive parents).</summary>
public interface IRoleService
{
    /// <summary>Defines a role (idempotent).</summary>
    /// <param name="role">The role name.</param>
    void Define(string role);

    /// <summary>Declares that a role inherits from a parent role, gaining the parent's roles and permissions.</summary>
    /// <param name="role">The child role.</param>
    /// <param name="parentRole">The parent role.</param>
    void AddParent(string role, string parentRole);

    /// <summary>Resolves the effective roles of a role: itself plus every ancestor (transitively).</summary>
    /// <param name="role">The role to expand.</param>
    /// <returns>The role and all inherited roles.</returns>
    IReadOnlyCollection<string> GetEffectiveRoles(string role);

    /// <summary>Expands a set of held roles into their union of effective roles.</summary>
    /// <param name="roles">The held roles.</param>
    /// <returns>The union of effective roles.</returns>
    IReadOnlyCollection<string> ExpandRoles(IEnumerable<string> roles);

    /// <summary>Determines whether a set of held roles satisfies a required role (through inheritance).</summary>
    /// <param name="heldRoles">The roles the principal holds.</param>
    /// <param name="requiredRole">The required role.</param>
    /// <returns><see langword="true"/> when the expanded held roles include the required role.</returns>
    bool IsInRole(IEnumerable<string> heldRoles, string requiredRole);
}

/// <summary>
/// Default in-memory <see cref="IRoleService"/>. Role inheritance is a directed graph walked breadth-first
/// (cycle-safe); results are cached. When <see cref="AuthorizationOptions.EnableRoleInheritance"/> is
/// <see langword="false"/>, a role resolves only to itself.
/// </summary>
public sealed class RoleService : IRoleService
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _parents =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IAuthorizationCache _cache;
    private readonly bool _inheritanceEnabled;

    /// <summary>Initializes a new instance of the <see cref="RoleService"/> class.</summary>
    /// <param name="cache">The authorization cache.</param>
    /// <param name="options">The authorization options.</param>
    public RoleService(IAuthorizationCache cache, IOptions<AuthorizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);

        _cache = cache;
        _inheritanceEnabled = options.Value.EnableRoleInheritance;
    }

    /// <inheritdoc />
    public void Define(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        _parents.GetOrAdd(role, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _cache.Clear();
    }

    /// <inheritdoc />
    public void AddParent(string role, string parentRole)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentRole);

        _parents.GetOrAdd(role, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(parentRole);
        _parents.GetOrAdd(parentRole, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _cache.Clear();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetEffectiveRoles(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        if (!_inheritanceEnabled)
        {
            return [role];
        }

        return _cache.GetOrAdd($"role:effective:{role}", () => Walk(role));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> ExpandRoles(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            expanded.UnionWith(GetEffectiveRoles(role));
        }

        return expanded;
    }

    /// <inheritdoc />
    public bool IsInRole(IEnumerable<string> heldRoles, string requiredRole)
    {
        ArgumentNullException.ThrowIfNull(heldRoles);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredRole);

        return ExpandRoles(heldRoles).Contains(requiredRole);
    }

    private string[] Walk(string role)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(role);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (_parents.TryGetValue(current, out var parents))
            {
                foreach (var parent in parents)
                {
                    queue.Enqueue(parent);
                }
            }
        }

        return [.. visited];
    }
}
