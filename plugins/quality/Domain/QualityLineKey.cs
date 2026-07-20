namespace FactoryOS.Plugins.Quality.Domain;

/// <summary>
/// Identifies the aggregate a defect rate is tracked for: a single product on a single line within a tenant.
/// Windows are per aggregate, never global — mirroring the per-aggregate event-ordering guarantee.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="LineId">The production line or workstation identifier.</param>
/// <param name="ProductId">The product identifier.</param>
public sealed record QualityLineKey(string Tenant, string LineId, string ProductId);
