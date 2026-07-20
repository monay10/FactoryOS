namespace FactoryOS.Identity.Authorization.Model;

/// <summary>A named grouping of related permission definitions (e.g. the <c>energy</c> module surface).</summary>
/// <param name="Key">The stable group key.</param>
/// <param name="Name">The human-readable group name.</param>
/// <param name="Description">An optional description.</param>
public sealed record PermissionGroup(string Key, string Name, string? Description = null);

/// <summary>
/// The catalog entry describing a single permission: its <see cref="Key"/> (a <c>resource.action</c> string),
/// the group it belongs to, and display metadata. Definitions are the surface an administrator assigns from.
/// </summary>
/// <param name="Key">The permission key (e.g. <c>energy.read</c>).</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="GroupKey">The owning <see cref="PermissionGroup.Key"/>, if any.</param>
/// <param name="Description">An optional description.</param>
public sealed record PermissionDefinition(
    string Key, string Name, string? GroupKey = null, string? Description = null);

/// <summary>The base of a permission grant (or denial) to a principal.</summary>
/// <param name="Permission">The permission key being assigned.</param>
/// <param name="IsGrant">Whether the assignment grants (<see langword="true"/>) or denies the permission.</param>
public abstract record PermissionAssignment(string Permission, bool IsGrant);

/// <summary>A permission granted to (or denied from) a role.</summary>
/// <param name="RoleName">The role the assignment applies to.</param>
/// <param name="Permission">The permission key.</param>
/// <param name="IsGrant">Whether the permission is granted (default) or denied.</param>
public sealed record RolePermission(string RoleName, string Permission, bool IsGrant = true)
    : PermissionAssignment(Permission, IsGrant);

/// <summary>A permission granted to (or explicitly denied from) an individual user.</summary>
/// <param name="UserId">The user the assignment applies to.</param>
/// <param name="Permission">The permission key.</param>
/// <param name="IsGrant">Whether the permission is granted (default) or denied. A denial overrides a role grant.</param>
public sealed record UserPermission(Guid UserId, string Permission, bool IsGrant = true)
    : PermissionAssignment(Permission, IsGrant);
