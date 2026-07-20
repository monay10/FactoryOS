using System.Collections.Concurrent;

namespace FactoryOS.Gateway.Branding;

/// <summary>
/// The default <see cref="ITenantBrandingProvider"/>: a per-tenant lookup seeded with any known branding and
/// falling back to neutral defaults (the tenant key as the display name) for a tenant it has not been told about.
/// A host binds real branding by seeding this from tenant configuration, or by registering its own provider.
/// Unknown tenants never fail — the shell always gets something to render.
/// </summary>
public sealed class TenantBrandingProvider : ITenantBrandingProvider
{
    private readonly ConcurrentDictionary<string, TenantBranding> _branding;

    /// <summary>Initializes a new instance seeded with the given per-tenant branding.</summary>
    /// <param name="seed">The known branding, keyed by tenant. May be empty.</param>
    public TenantBrandingProvider(IEnumerable<TenantBranding>? seed = null)
    {
        _branding = new ConcurrentDictionary<string, TenantBranding>(StringComparer.OrdinalIgnoreCase);
        if (seed is not null)
        {
            foreach (var branding in seed)
            {
                _branding[branding.Tenant] = branding;
            }
        }
    }

    /// <inheritdoc />
    public TenantBranding ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _branding.TryGetValue(tenant, out var branding)
            ? branding
            : new TenantBranding(tenant, tenant);
    }
}
