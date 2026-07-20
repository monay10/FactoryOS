namespace FactoryOS.Plugins.Energy.Domain;

/// <summary>
/// Identifies the aggregate a baseline is tracked for: a single metric of a single meter within a tenant.
/// Ordering and baselines are per aggregate, never global — mirroring the event-ordering guarantee.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="MeterId">The meter or sensor identifier.</param>
/// <param name="Metric">The measured metric (for example <c>ActivePower</c>).</param>
public sealed record EnergyMeterKey(string Tenant, string MeterId, string Metric);
