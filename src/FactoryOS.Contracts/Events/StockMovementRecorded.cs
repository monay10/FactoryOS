namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that stock moved for an item in a warehouse: a signed quantity delta (positive for receipts,
/// negative for issues). Any module consumes it without referencing the producer; the Warehouse module applies
/// it to the on-hand ledger. The item is identified by its Standard Model SKU, never an ERP dialect.
/// </summary>
public sealed record StockMovementRecorded : IntegrationEvent
{
    /// <summary>The tenant the warehouse belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The warehouse (or location) the movement occurred in.</summary>
    public required string WarehouseId { get; init; }

    /// <summary>The item that moved, by its Standard Model SKU.</summary>
    public required string Sku { get; init; }

    /// <summary>The signed change in on-hand quantity: positive for a receipt, negative for an issue.</summary>
    public decimal QuantityDelta { get; init; }

    /// <summary>When the movement occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}
