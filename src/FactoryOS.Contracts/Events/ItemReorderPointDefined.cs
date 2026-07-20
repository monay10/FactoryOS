namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a reorder point was set for an item in a warehouse — the on-hand level at or below which
/// the item is considered low. The Warehouse module records it as the threshold for low-stock detection; when no
/// point is defined, the module's configured default applies.
/// </summary>
public sealed record ItemReorderPointDefined : IntegrationEvent
{
    /// <summary>The tenant the warehouse belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The warehouse (or location) the reorder point applies in.</summary>
    public required string WarehouseId { get; init; }

    /// <summary>The item the reorder point is for, by its Standard Model SKU.</summary>
    public required string Sku { get; init; }

    /// <summary>The on-hand quantity at or below which the item is low. Non-positive disables alerting for the item.</summary>
    public decimal ReorderPoint { get; init; }
}
