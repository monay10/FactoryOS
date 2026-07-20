namespace FactoryOS.Plugins.Warehouse.Domain;

/// <summary>A read-model snapshot of an item's on-hand level and reorder point in a warehouse.</summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="WarehouseId">The warehouse or location.</param>
/// <param name="Sku">The Standard Model SKU.</param>
/// <param name="OnHand">The current on-hand quantity.</param>
/// <param name="ReorderPoint">The item's reorder point, or <see langword="null"/> if none is set.</param>
public sealed record StockLevel(
    string Tenant,
    string WarehouseId,
    string Sku,
    decimal OnHand,
    decimal? ReorderPoint);
