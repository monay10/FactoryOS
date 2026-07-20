namespace FactoryOS.Gateway.Tenancy;

/// <summary>The gateway's view of the tenant it resolved for the current request.</summary>
/// <param name="Resolved">Whether a tenant was resolved from the request.</param>
/// <param name="Tenant">The resolved tenant, or <see langword="null"/> when none was resolved.</param>
internal sealed record TenantContextResponse(bool Resolved, string? Tenant);
