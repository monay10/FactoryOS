namespace FactoryOS.Gateway.Branding;

/// <summary>
/// Resolves a tenant's <see cref="TenantBranding"/> by tenant key. The default implementation returns neutral
/// branding (the tenant key as the display name); a host binds real per-tenant branding from tenant configuration
/// by registering its own implementation — the gateway stays free of any customer-specific data.
/// </summary>
public interface ITenantBrandingProvider
{
    /// <summary>Gets the branding for a tenant, always returning a value (neutral defaults when unknown).</summary>
    /// <param name="tenant">The tenant key.</param>
    /// <returns>The tenant's branding.</returns>
    TenantBranding ForTenant(string tenant);
}
