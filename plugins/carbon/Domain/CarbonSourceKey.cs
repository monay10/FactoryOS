namespace FactoryOS.Plugins.Carbon.Domain;

/// <summary>
/// Identifies the aggregate a cumulative emission is tracked for: a single source (meter) within a tenant.
/// Totals are per aggregate, never global — mirroring the per-aggregate event-ordering guarantee.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="Source">The energy source (meter) identifier.</param>
public sealed record CarbonSourceKey(string Tenant, string Source);
