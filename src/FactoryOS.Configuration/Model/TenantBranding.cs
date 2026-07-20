namespace FactoryOS.Configuration.Model;

/// <summary>Per-tenant branding, so each factory presents its own identity.</summary>
/// <param name="DisplayName">The tenant's display name shown in the UI.</param>
/// <param name="PrimaryColor">An optional primary theme color (e.g. a hex string).</param>
/// <param name="LogoUrl">An optional logo URL.</param>
public sealed record TenantBranding(string DisplayName, string? PrimaryColor = null, string? LogoUrl = null);
