namespace FactoryOS.Plugins.Warehouse;

/// <summary>
/// Configuration for the Warehouse module. Behaviour varies by configuration, never by customer branch: a
/// factory sets the reorder point used for items that have no explicit one.
/// </summary>
public sealed record WarehouseOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Warehouse";

    /// <summary>
    /// The reorder point applied to items without an explicit <c>ItemReorderPointDefined</c>. Non-positive
    /// (the default) means such items never raise a low-stock alert until a point is defined.
    /// </summary>
    public decimal DefaultReorderPoint { get; init; }
}
