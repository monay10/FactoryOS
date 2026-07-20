using System.Diagnostics.CodeAnalysis;

namespace FactoryOS.Gateway.Tenancy;

/// <summary>
/// The tenant resolved for the current request. Multi-tenancy is a construction rule of FactoryOS: every
/// request runs on behalf of exactly one factory. This request-scoped context carries that tenant so a
/// module endpoint reads it once from the ambient scope instead of re-parsing and re-validating a
/// <c>tenant</c> parameter on every route.
/// </summary>
public interface ITenantContext
{
    /// <summary>Gets a value indicating whether a tenant was resolved for the current request.</summary>
    bool HasTenant { get; }

    /// <summary>Gets the resolved tenant. Throws when none was resolved; guard with <see cref="HasTenant"/>.</summary>
    /// <exception cref="InvalidOperationException">No tenant was resolved for the current request.</exception>
    string Tenant { get; }

    /// <summary>Attempts to read the resolved tenant without throwing.</summary>
    /// <param name="tenant">The resolved tenant when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a tenant was resolved; otherwise <see langword="false"/>.</returns>
    bool TryGetTenant([NotNullWhen(true)] out string? tenant);
}
