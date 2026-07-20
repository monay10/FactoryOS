using FactoryOS.Identity.Domain;

namespace FactoryOS.Identity.Persistence;

/// <summary>Stores and retrieves users.</summary>
public interface IUserStore
{
    /// <summary>Adds a user.</summary>
    /// <param name="user">The user to add.</param>
    void Add(User user);

    /// <summary>Finds a user by tenant and user name.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userName">The user name.</param>
    /// <returns>The user, or <see langword="null"/> when not found.</returns>
    User? FindByUserName(Guid tenantId, string userName);

    /// <summary>Finds a user by identifier.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The user, or <see langword="null"/> when not found.</returns>
    User? FindById(Guid userId);
}
