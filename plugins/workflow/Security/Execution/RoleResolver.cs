using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Persistence;

namespace FactoryOS.Plugins.Workflow.Security.Execution;

/// <summary>
/// Expands the roles a principal presented into every role those roles include.
/// <para>
/// Role inheritance is what stops "a plant manager is everything an operator is, plus more" from being written
/// out twice and drifting apart the first time an operator gains a permission. Expansion is <b>breadth-first
/// with a visited set</b>, so a role graph that accidentally contains a cycle resolves to a finite set instead
/// of hanging the request — a configuration mistake should not be able to take authorization down.
/// </para>
/// </summary>
public sealed class RoleResolver
{
    private readonly ISecurityRepository _repository;

    /// <summary>Initializes a new instance of the <see cref="RoleResolver"/> class.</summary>
    /// <param name="repository">The role registry.</param>
    public RoleResolver(ISecurityRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>Expands a set of role keys into every role reachable from them.</summary>
    /// <param name="roleKeys">The roles as presented.</param>
    /// <returns>The effective roles, including the ones presented.</returns>
    public IReadOnlyList<SecurityRole> Expand(IEnumerable<string> roleKeys)
    {
        ArgumentNullException.ThrowIfNull(roleKeys);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<SecurityRole>();
        var queue = new Queue<string>(roleKeys);

        while (queue.TryDequeue(out var key))
        {
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
            {
                continue;
            }

            // A role the principal presented but that nobody registered contributes nothing. That is the safe
            // direction: an unknown role granting nothing is a visible gap, while an unknown role granting
            // something would be an invisible one.
            if (_repository.FindRole(key) is not { } role)
            {
                continue;
            }

            resolved.Add(role);
            foreach (var included in role.Includes)
            {
                queue.Enqueue(included);
            }
        }

        return resolved;
    }

    /// <summary>Gets the keys of every role reachable from the ones presented.</summary>
    /// <param name="roleKeys">The roles as presented.</param>
    /// <returns>The effective role keys, including any presented role that is not registered.</returns>
    public IReadOnlyList<string> ExpandKeys(IEnumerable<string> roleKeys)
    {
        ArgumentNullException.ThrowIfNull(roleKeys);

        var presented = roleKeys.Where(key => !string.IsNullOrWhiteSpace(key)).ToArray();
        var keys = new HashSet<string>(presented, StringComparer.OrdinalIgnoreCase);
        foreach (var role in Expand(presented))
        {
            keys.Add(role.Key);
        }

        return keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Gets every permission carried by a set of roles and the roles they include.</summary>
    /// <param name="roleKeys">The roles as presented.</param>
    /// <returns>The permission strings.</returns>
    public IReadOnlyList<string> PermissionsOf(IEnumerable<string> roleKeys) =>
        Expand(roleKeys)
            .SelectMany(role => role.Permissions)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToArray();
}
