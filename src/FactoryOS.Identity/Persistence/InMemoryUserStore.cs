using System.Collections.Concurrent;
using FactoryOS.Identity.Domain;

namespace FactoryOS.Identity.Persistence;

/// <summary>An in-memory <see cref="IUserStore"/> for development and tests.</summary>
public sealed class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    /// <inheritdoc />
    public void Add(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _users[user.Id] = user;
    }

    /// <inheritdoc />
    public User? FindByUserName(Guid tenantId, string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        return _users.Values.FirstOrDefault(user =>
            user.TenantId == tenantId
            && string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public User? FindById(Guid userId) => _users.TryGetValue(userId, out var user) ? user : null;
}
