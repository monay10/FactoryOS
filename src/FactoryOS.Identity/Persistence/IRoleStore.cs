using FactoryOS.Identity.Domain;

namespace FactoryOS.Identity.Persistence;

/// <summary>Stores and retrieves roles.</summary>
public interface IRoleStore
{
    /// <summary>Adds a role.</summary>
    /// <param name="role">The role to add.</param>
    void Add(Role role);

    /// <summary>Finds the roles matching the supplied identifiers.</summary>
    /// <param name="roleIds">The role identifiers.</param>
    /// <returns>The matching roles.</returns>
    IReadOnlyCollection<Role> FindByIds(IEnumerable<Guid> roleIds);
}
