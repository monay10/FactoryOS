using FactoryOS.Identity.Authorization;
using FactoryOS.Identity.Credentials;
using FactoryOS.Identity.Domain;
using FactoryOS.Identity.Persistence;

namespace FactoryOS.Identity.Seeding;

/// <summary>The outcome of a seed run.</summary>
/// <param name="Roles">How many roles were created.</param>
/// <param name="Users">How many users were created (zero when no password was supplied).</param>
public sealed record IdentitySeedResult(int Roles, int Users);

/// <summary>
/// Seeds the default FactoryOS roles and a demo user per role into the identity stores. The roles and their
/// permission grants are data (the same permission surface the module manifests declare, using the wildcard
/// convention), so a seeded session's token carries exactly the permissions that shape its navigation. No
/// credential is hard-coded — users are seeded only when a password is supplied by configuration.
/// </summary>
public sealed class DefaultIdentitySeeder
{
    // The default role → permission grants. "*" is super-admin; "resource.*" grants every action on a module.
    private static readonly (string Role, string[] Permissions)[] RoleGrants =
    [
        ("Administrator", ["*"]),
        ("PlantSupervisor", ["dashboard.view", "oee.view", "energy.view", "maintenance.view", "quality.view", "warehouse.view", "activity.view", "brain.view"]),
        ("EnergyOperator", ["dashboard.view", "energy.*", "activity.view"]),
        ("QualityInspector", ["dashboard.view", "quality.*", "activity.view"]),
    ];

    // One demo user per role, so each role can be exercised end-to-end.
    private static readonly (string UserName, string Role)[] UserRoles =
    [
        ("admin", "Administrator"),
        ("supervisor", "PlantSupervisor"),
        ("energy", "EnergyOperator"),
        ("quality", "QualityInspector"),
    ];

    private readonly IUserStore _users;
    private readonly IRoleStore _roles;
    private readonly IPasswordHasher _passwordHasher;

    /// <summary>Initializes a new instance of the <see cref="DefaultIdentitySeeder"/> class.</summary>
    /// <param name="users">The user store.</param>
    /// <param name="roles">The role store.</param>
    /// <param name="passwordHasher">The password hasher.</param>
    public DefaultIdentitySeeder(IUserStore users, IRoleStore roles, IPasswordHasher passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        _users = users;
        _roles = roles;
        _passwordHasher = passwordHasher;
    }

    /// <summary>Seeds the default roles (and demo users when a password is supplied) under the seed tenant.</summary>
    /// <param name="options">The seed options carrying the tenant and demo password.</param>
    /// <returns>How many roles and users were created.</returns>
    public IdentitySeedResult Seed(IdentitySeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var roleIdsByName = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var (roleName, permissions) in RoleGrants)
        {
            var role = Role.Create(Guid.NewGuid(), options.TenantId, roleName);
            foreach (var permission in permissions)
            {
                role.Grant(Permission.Parse(permission));
            }

            _roles.Add(role);
            roleIdsByName[roleName] = role.Id;
        }

        // No password → seed roles only, never invent a credential.
        if (string.IsNullOrWhiteSpace(options.Password))
        {
            return new IdentitySeedResult(RoleGrants.Length, 0);
        }

        var passwordHash = _passwordHasher.Hash(options.Password);
        var seededUsers = 0;
        foreach (var (userName, roleName) in UserRoles)
        {
            var user = User.Create(
                Guid.NewGuid(),
                options.TenantId,
                userName,
                $"{userName}@factoryos.local",
                passwordHash);
            user.AssignRole(roleIdsByName[roleName]);
            _users.Add(user);
            seededUsers++;
        }

        return new IdentitySeedResult(RoleGrants.Length, seededUsers);
    }
}
