namespace FactoryOS.Gateway.Tenancy;

/// <summary>
/// Configures how the gateway resolves the tenant of an incoming request. The header is the primary
/// source (the tenant of the authenticated session); the query key is a fallback for tools, links and
/// tests. Both are configuration — the core never hard-codes a tenant.
/// </summary>
public sealed class TenantResolutionOptions
{
    /// <summary>The configuration section these options bind from (<c>Gateway:TenantResolution</c>).</summary>
    public const string SectionName = "Gateway:TenantResolution";

    /// <summary>Gets or sets the request header carrying the tenant. Defaults to <c>X-FactoryOS-Tenant</c>.</summary>
    public string HeaderName { get; set; } = "X-FactoryOS-Tenant";

    /// <summary>Gets or sets the query-string key used as a fallback tenant source. Defaults to <c>tenant</c>.</summary>
    public string QueryFallbackKey { get; set; } = "tenant";
}
