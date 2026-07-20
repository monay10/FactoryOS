namespace FactoryOS.Contracts.StandardModel;

/// <summary>
/// Canonical representation of a stock item. Vendor dialects such as <c>LogoStock</c>,
/// <c>SAP.Material</c> and <c>Netsis.ItemCard</c> all normalize into this single entity.
/// </summary>
public sealed record InventoryItem : IStandardEntity
{
    /// <summary>The canonical entity type name.</summary>
    public const string Type = "InventoryItem";

    /// <inheritdoc />
    public required string Tenant { get; init; }

    /// <summary>Gets the stock-keeping unit; the natural key of the item within a tenant.</summary>
    public required string Sku { get; init; }

    /// <summary>Gets the human-readable item name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the quantity on hand, in <see cref="Unit"/>.</summary>
    public decimal Quantity { get; init; }

    /// <summary>Gets the unit of measure the quantity is expressed in (for example <c>kg</c> or <c>pcs</c>).</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>Gets the storage location the stock is held at, if known.</summary>
    public string? Location { get; init; }

    /// <inheritdoc />
    public string EntityType => Type;

    /// <inheritdoc />
    public string NaturalKey => Sku;
}
