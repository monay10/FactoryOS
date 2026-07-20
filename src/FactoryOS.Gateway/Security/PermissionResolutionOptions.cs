namespace FactoryOS.Gateway.Security;

/// <summary>
/// Configures how the gateway resolves the caller's permissions. The header carries the permissions of the
/// authenticated session (a real identity provider would populate it from the user's roles). Absent header means
/// no identity was presented, and the request is treated as unrestricted — configuration, never a customer branch.
/// </summary>
public sealed class PermissionResolutionOptions
{
    /// <summary>The configuration section these options bind from (<c>Gateway:PermissionResolution</c>).</summary>
    public const string SectionName = "Gateway:PermissionResolution";

    /// <summary>The request header carrying the caller's permissions. Defaults to <c>X-FactoryOS-Permissions</c>.</summary>
    public string HeaderName { get; set; } = "X-FactoryOS-Permissions";

    /// <summary>
    /// The claim type that carries a permission on an authenticated principal. Defaults to
    /// <c>factoryos:permission</c> — the type the FactoryOS Identity layer issues — so a validated access token
    /// drives navigation without the gateway referencing Identity.
    /// </summary>
    public string PermissionClaimType { get; set; } = "factoryos:permission";
}
