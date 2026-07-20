using System.Diagnostics.CodeAnalysis;

namespace FactoryOS.Gateway.Tenancy;

/// <summary>
/// The mutable, request-scoped backing store for <see cref="ITenantContext"/>. Only the tenant-resolution
/// middleware writes to it, at the start of the request; endpoints only read. It is registered as a scoped
/// service so each request gets its own isolated instance and tenants can never bleed across requests.
/// </summary>
internal sealed class TenantContext : ITenantContext
{
    private string? _tenant;

    public bool HasTenant => _tenant is not null;

    public string Tenant =>
        _tenant ?? throw new InvalidOperationException("No tenant has been resolved for the current request.");

    public bool TryGetTenant([NotNullWhen(true)] out string? tenant)
    {
        tenant = _tenant;
        return _tenant is not null;
    }

    /// <summary>Records the tenant resolved for the current request. Invoked once by the middleware.</summary>
    /// <param name="tenant">The non-empty tenant identifier.</param>
    internal void Set(string tenant) => _tenant = tenant;
}
