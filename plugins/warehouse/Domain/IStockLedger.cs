namespace FactoryOS.Plugins.Warehouse.Domain;

/// <summary>
/// Maintains per-tenant on-hand levels and reorder points, keyed by warehouse and SKU. Applying a movement
/// reports the level before and after so callers can detect a downward crossing. Tenant-scoped through the key.
/// </summary>
public interface IStockLedger
{
    /// <summary>Applies a signed quantity delta to an item's on-hand level.</summary>
    /// <param name="key">The aggregate the movement is for.</param>
    /// <param name="quantityDelta">The signed change: positive for a receipt, negative for an issue.</param>
    /// <returns>The on-hand level before and after the movement.</returns>
    StockChange Apply(WarehouseStockKey key, decimal quantityDelta);

    /// <summary>Sets the reorder point for an item, replacing any prior value.</summary>
    /// <param name="key">The aggregate the point is for.</param>
    /// <param name="reorderPoint">The reorder point.</param>
    void SetReorderPoint(WarehouseStockKey key, decimal reorderPoint);

    /// <summary>Returns the reorder point for an item, or <see langword="null"/> if none is set.</summary>
    /// <param name="key">The aggregate to look up.</param>
    /// <returns>The reorder point, or <see langword="null"/>.</returns>
    decimal? GetReorderPoint(WarehouseStockKey key);

    /// <summary>Returns the stock levels of all tracked items for a tenant.</summary>
    /// <param name="tenant">The tenant to list within.</param>
    /// <returns>The tenant's stock-level snapshots.</returns>
    IReadOnlyCollection<StockLevel> ForTenant(string tenant);
}
