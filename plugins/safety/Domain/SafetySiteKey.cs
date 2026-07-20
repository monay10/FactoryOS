namespace FactoryOS.Plugins.Safety.Domain;

/// <summary>
/// Identifies the aggregate an incident window is tracked for: a single site within a tenant. Windows are per
/// aggregate, never global — mirroring the per-aggregate event-ordering guarantee.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="SiteId">The site, area or line identifier.</param>
public sealed record SafetySiteKey(string Tenant, string SiteId);
