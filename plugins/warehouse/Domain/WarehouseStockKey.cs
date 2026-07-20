namespace FactoryOS.Plugins.Warehouse.Domain;

/// <summary>
/// Identifies the aggregate an on-hand level is tracked for: a single SKU in a single warehouse within a
/// tenant. Levels are per aggregate, never global — mirroring the per-aggregate event-ordering guarantee.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="WarehouseId">The warehouse or location identifier.</param>
/// <param name="Sku">The Standard Model SKU.</param>
public sealed record WarehouseStockKey(string Tenant, string WarehouseId, string Sku);
