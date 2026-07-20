namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that an item's on-hand quantity crossed down to or below its reorder point. Emitted only on
/// the downward crossing, not on every movement while already low. Procurement, maintenance, notification,
/// dashboards and AI agents consume it without referencing the Warehouse module.
/// </summary>
public sealed record LowStockDetected : IntegrationEvent
{
    /// <summary>The tenant the warehouse belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The warehouse (or location) the item is low in.</summary>
    public required string WarehouseId { get; init; }

    /// <summary>The item that is low, by its Standard Model SKU.</summary>
    public required string Sku { get; init; }

    /// <summary>The on-hand quantity after the movement that triggered the alert.</summary>
    public decimal OnHand { get; init; }

    /// <summary>The reorder point the on-hand quantity crossed.</summary>
    public decimal ReorderPoint { get; init; }

    /// <summary>When the triggering movement occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>The id of the <see cref="StockMovementRecorded"/> that triggered this alert.</summary>
    public Guid SourceEventId { get; init; }
}
