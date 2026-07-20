using System.Security.Claims;
using FactoryOS.Domain.Results;
using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Claims;
using FactoryOS.Identity.Configuration;
using FactoryOS.Identity.Credentials;
using FactoryOS.Identity.Domain;
using FactoryOS.Identity.Persistence;
using FactoryOS.Identity.Policies;
using Microsoft.Extensions.Options;

namespace FactoryOS.Identity.Services;

/// <summary>
/// The identity foundation façade: registers users (enforcing the password policy and tenant/uniqueness
/// rules), changes credentials, and resolves a user's effective roles, permissions and claims. It composes
/// the existing stores, password hasher and claim factory — it does not replace them.
/// </summary>
public interface IIdentityService
{
    /// <summary>Registers a new user, hashing the password after validating it against the policy.</summary>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="userName">The user name (unique per tenant when required).</param>
    /// <param name="email">The email address.</param>
    /// <param name="password">The plaintext password.</param>
    /// <param name="organizationId">The optional organization.</param>
    /// <returns>A successful result with the created user, or a validation/conflict failure.</returns>
    Result<User> RegisterUser(
        Guid tenantId, string userName, string email, string password, Guid? organizationId = null);

    /// <summary>Changes a user's password after validating the new value against the policy.</summary>
    /// <param name="user">The user whose password changes.</param>
    /// <param name="newPassword">The new plaintext password.</param>
    /// <returns>A successful result, or a validation failure.</returns>
    Result ChangePassword(User user, string newPassword);

    /// <summary>Finds a user by identifier.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The user, or <see langword="null"/> when not found.</returns>
    User? FindUser(Guid userId);

    /// <summary>Resolves the roles assigned to a user.</summary>
    /// <param name="user">The user.</param>
    /// <returns>The user's roles.</returns>
    IReadOnlyCollection<Role> ResolveRoles(User user);

    /// <summary>Resolves a user's effective permissions (the distinct union across their roles).</summary>
    /// <param name="user">The user.</param>
    /// <returns>The effective permissions.</returns>
    IReadOnlyCollection<Permission> ResolvePermissions(User user);

    /// <summary>Resolves the claim set describing a user (subject, tenant, roles, permissions).</summary>
    /// <param name="user">The user.</param>
    /// <returns>The ordered claim list.</returns>
    IReadOnlyList<Claim> ResolveClaims(User user);
}

/// <summary>Default <see cref="IIdentityService"/>.</summary>
public sealed class IdentityService : IIdentityService
{
    private readonly IUserStore _users;
    private readonly IRoleStore _roles;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicy _passwordPolicy;
    private readonly IdentityOptions _options;

    /// <summary>Initializes a new instance of the <see cref="IdentityService"/> class.</summary>
    /// <param name="users">The user store.</param>
    /// <param name="roles">The role store.</param>
    /// <param name="passwordHasher">The password hasher.</param>
    /// <param name="passwordPolicy">The password policy.</param>
    /// <param name="options">The identity options.</param>
    public IdentityService(
        IUserStore users,
        IRoleStore roles,
        IPasswordHasher passwordHasher,
        IPasswordPolicy passwordPolicy,
        IOptions<IdentityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(passwordPolicy);
        ArgumentNullException.ThrowIfNull(options);

        _users = users;
        _roles = roles;
        _passwordHasher = passwordHasher;
        _passwordPolicy = passwordPolicy;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Result<User> RegisterUser(
        Guid tenantId, string userName, string email, string password, Guid? organizationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (_options.RequireTenantScope && tenantId == Guid.Empty)
        {
            return Result.Failure<User>(Error.Validation(
                "Identity.User.TenantRequired", "A tenant is required to register a user."));
        }

        var policy = _passwordPolicy.Validate(password);
        if (policy.IsFailure)
        {
            return Result.Failure<User>(policy.Error);
        }

        if (_options.RequireUniqueUserName && _users.FindByUserName(tenantId, userName) is not null)
        {
            return Result.Failure<User>(Error.Conflict(
                "Identity.User.Duplicate", "A user with the same name already exists in this tenant."));
        }

        var user = User.Create(
            Guid.NewGuid(), tenantId, userName, email, _passwordHasher.Hash(password), organizationId);
        _users.Add(user);
        return user;
    }

    /// <inheritdoc />
    public Result ChangePassword(User user, string newPassword)
    {
        ArgumentNullException.ThrowIfNull(user);

        var policy = _passwordPolicy.Validate(newPassword);
        if (policy.IsFailure)
        {
            return policy;
        }

        user.ChangePassword(_passwordHasher.Hash(newPassword));
        return Result.Success();
    }

    /// <inheritdoc />
    public User? FindUser(Guid userId) => _users.FindById(userId);

    /// <inheritdoc />
    public IReadOnlyCollection<Role> ResolveRoles(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return _roles.FindByIds(user.RoleIds);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Permission> ResolvePermissions(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return ResolveRoles(user)
            .SelectMany(role => role.Permissions)
            .Distinct()
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<Claim> ResolveClaims(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var roles = ResolveRoles(user);
        var permissions = roles.SelectMany(role => role.Permissions).Distinct();
        return ClaimsFactory.Create(user, roles.Select(role => role.Name), permissions);
    }
}
