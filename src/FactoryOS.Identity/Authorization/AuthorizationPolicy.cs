namespace FactoryOS.Identity.Authorization;

/// <summary>
/// A named authorization policy: the set of permissions a principal must hold, either all of them
/// (<see cref="RequireAll"/> is <see langword="true"/>) or at least one.
/// </summary>
/// <param name="Name">The policy name.</param>
/// <param name="Permissions">The permissions the policy requires.</param>
/// <param name="RequireAll">Whether all permissions are required (<see langword="true"/>) or any one suffices.</param>
public sealed record AuthorizationPolicy(string Name, IReadOnlyList<string> Permissions, bool RequireAll = true)
{
    /// <summary>Creates a policy requiring every listed permission.</summary>
    /// <param name="name">The policy name.</param>
    /// <param name="permissions">The required permissions.</param>
    /// <returns>The policy.</returns>
    public static AuthorizationPolicy RequireAllOf(string name, params string[] permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(permissions);
        return new AuthorizationPolicy(name, permissions, RequireAll: true);
    }

    /// <summary>Creates a policy satisfied by any one of the listed permissions.</summary>
    /// <param name="name">The policy name.</param>
    /// <param name="permissions">The candidate permissions.</param>
    /// <returns>The policy.</returns>
    public static AuthorizationPolicy RequireAnyOf(string name, params string[] permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(permissions);
        return new AuthorizationPolicy(name, permissions, RequireAll: false);
    }
}
