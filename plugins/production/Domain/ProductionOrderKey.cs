namespace FactoryOS.Plugins.Production.Domain;

/// <summary>
/// Identifies a production order within a tenant. Progress is tracked per aggregate, never global — mirroring
/// the per-aggregate event-ordering guarantee (an order's release precedes its counts).
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OrderId">The production order identifier.</param>
public sealed record ProductionOrderKey(string Tenant, string OrderId);
