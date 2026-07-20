using FactoryOS.Domain.Primitives;

namespace FactoryOS.Identity.Domain;

/// <summary>A user account within a tenant, holding credentials and role assignments.</summary>
public sealed class User : AggregateRoot<Guid>
{
    private readonly HashSet<Guid> _roleIds = [];

    private User(Guid id, Guid tenantId, string userName, string email, string passwordHash, Guid? organizationId)
        : base(id)
    {
        TenantId = tenantId;
        UserName = userName;
        Email = email;
        PasswordHash = passwordHash;
        OrganizationId = organizationId;
        IsActive = true;
    }

    private User()
    {
        UserName = string.Empty;
        Email = string.Empty;
        PasswordHash = string.Empty;
    }

    /// <summary>Gets the owning tenant identifier.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the organization the user belongs to, if any.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Gets the unique (per tenant) user name.</summary>
    public string UserName { get; private set; }

    /// <summary>Gets the user's email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the hashed password.</summary>
    public string PasswordHash { get; private set; }

    /// <summary>Gets a value indicating whether the account is active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the identifiers of the roles assigned to the user.</summary>
    public IReadOnlyCollection<Guid> RoleIds => _roleIds;

    /// <summary>Creates a new user.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="email">The email address.</param>
    /// <param name="passwordHash">The pre-hashed password.</param>
    /// <param name="organizationId">The optional organization.</param>
    /// <returns>The new user.</returns>
    public static User Create(
        Guid id,
        Guid tenantId,
        string userName,
        string email,
        string passwordHash,
        Guid? organizationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return new User(id, tenantId, userName, email, passwordHash, organizationId);
    }

    /// <summary>Assigns a role to the user.</summary>
    /// <param name="roleId">The role identifier.</param>
    public void AssignRole(Guid roleId) => _roleIds.Add(roleId);

    /// <summary>Removes a role from the user.</summary>
    /// <param name="roleId">The role identifier.</param>
    /// <returns><see langword="true"/> when the role was assigned and removed.</returns>
    public bool RemoveRole(Guid roleId) => _roleIds.Remove(roleId);

    /// <summary>Replaces the user's password hash.</summary>
    /// <param name="passwordHash">The new pre-hashed password.</param>
    public void ChangePassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    /// <summary>Deactivates the account.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reactivates the account.</summary>
    public void Activate() => IsActive = true;
}
