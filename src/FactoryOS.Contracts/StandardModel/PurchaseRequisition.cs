namespace FactoryOS.Contracts.StandardModel;

/// <summary>
/// Canonical representation of a purchase requisition (a request to replenish an item), shared by the
/// Procurement, Warehouse and Workflow modules. Connectors normalize ERP requisition dialects into this entity.
/// </summary>
public sealed record PurchaseRequisition : IStandardEntity
{
    /// <summary>The canonical entity type name.</summary>
    public const string Type = "PurchaseRequisition";

    /// <inheritdoc />
    public required string Tenant { get; init; }

    /// <summary>Gets the requisition number; the natural key within a tenant.</summary>
    public required string Number { get; init; }

    /// <summary>Gets the item to replenish, by its Standard Model SKU.</summary>
    public required string Sku { get; init; }

    /// <summary>Gets the warehouse or location the requisition is for.</summary>
    public required string WarehouseId { get; init; }

    /// <summary>Gets the requested replenishment quantity.</summary>
    public decimal RequestedQuantity { get; init; }

    /// <summary>Gets the current status (for example <c>Draft</c>, <c>Approved</c> or <c>Ordered</c>).</summary>
    public string Status { get; init; } = string.Empty;

    /// <inheritdoc />
    public string EntityType => Type;

    /// <inheritdoc />
    public string NaturalKey => Number;
}
