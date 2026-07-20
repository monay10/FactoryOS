using System.Collections.Concurrent;
using FactoryOS.Identity.Domain;

namespace FactoryOS.Identity.Persistence;

/// <summary>An in-memory <see cref="IRoleStore"/> for development and tests.</summary>
public sealed class InMemoryRoleStore : IRoleStore
{
    private readonly ConcurrentDictionary<Guid, Role> _roles = new();

    /// <inheritdoc />
    public void Add(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        _roles[role.Id] = role;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Role> FindByIds(IEnumerable<Guid> roleIds)
    {
        ArgumentNullException.ThrowIfNull(roleIds);

        var result = new List<Role>();
        foreach (var roleId in roleIds)
        {
            if (_roles.TryGetValue(roleId, out var role))
            {
                result.Add(role);
            }
        }

        return result;
    }
}
