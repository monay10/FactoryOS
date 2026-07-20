namespace FactoryOS.Identity.Authorization.Configuration;

/// <summary>
/// Stable constants for the FactoryOS authorization foundation: configuration section names and the
/// permission-hierarchy tokens shared by the evaluator, services and sample configuration.
/// </summary>
public static class AuthorizationConstants
{
    /// <summary>The root configuration section the <see cref="AuthorizationOptions"/> bind from.</summary>
    public const string ConfigurationSection = "Authorization";

    /// <summary>The configuration section named policies bind from.</summary>
    public const string PoliciesSection = "Authorization:Policies";

    /// <summary>The configuration section the permission cache binds from.</summary>
    public const string PermissionCacheSection = "Authorization:PermissionCache";

    /// <summary>The wildcard token matching any permission segment (and, alone, every permission).</summary>
    public const string Wildcard = "*";

    /// <summary>The separator between permission hierarchy segments (e.g. <c>energy.read</c>).</summary>
    public const char HierarchySeparator = '.';

    /// <summary>The default permission/role/policy cache time-to-live, in seconds.</summary>
    public const int DefaultCacheTtlSeconds = 300;
}

/// <summary>Settings for a single named policy, bound from <see cref="AuthorizationConstants.PoliciesSection"/>.</summary>
public sealed class PolicySettings
{
    /// <summary>Gets the permissions the policy requires.</summary>
    public IList<string> Permissions { get; } = [];

    /// <summary>Gets or sets a value indicating whether all permissions are required (otherwise any one suffices).</summary>
    public bool RequireAll { get; set; } = true;
}

/// <summary>The permission/role/policy cache policy, bound from <see cref="AuthorizationConstants.PermissionCacheSection"/>.</summary>
public sealed class PermissionCacheOptions
{
    /// <summary>Gets or sets a value indicating whether authorization caching is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the cache entry time-to-live, in seconds.</summary>
    public int TtlSeconds { get; set; } = AuthorizationConstants.DefaultCacheTtlSeconds;
}

/// <summary>
/// The authorization foundation options, bound from <see cref="AuthorizationConstants.ConfigurationSection"/>.
/// The <see cref="PermissionCache"/> and <see cref="Policies"/> children map to their nested sections.
/// </summary>
public sealed class AuthorizationOptions
{
    /// <summary>Gets or sets a value indicating whether role inheritance is honoured during evaluation.</summary>
    public bool EnableRoleInheritance { get; set; } = true;

    /// <summary>Gets or sets the permission/role/policy cache policy.</summary>
    public PermissionCacheOptions PermissionCache { get; set; } = new();

    /// <summary>Gets the named policies declared in configuration, keyed by policy name.</summary>
    public IDictionary<string, PolicySettings> Policies { get; } =
        new Dictionary<string, PolicySettings>(StringComparer.Ordinal);
}
