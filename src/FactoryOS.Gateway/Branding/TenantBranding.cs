namespace FactoryOS.Gateway.Branding;

/// <summary>
/// A tenant's presentation identity, delivered to the shell so each factory themes itself (Law 6: branding is
/// per-tenant). It is plain data resolved by the resolved tenant — never a core code branch on a customer.
/// </summary>
/// <param name="Tenant">The tenant this branding is for.</param>
/// <param name="DisplayName">The factory's display name shown in the shell.</param>
/// <param name="PrimaryColor">An optional primary theme color (a CSS color string, e.g. a hex).</param>
/// <param name="LogoUrl">An optional logo URL.</param>
public sealed record TenantBranding(string Tenant, string DisplayName, string? PrimaryColor = null, string? LogoUrl = null);
