namespace FactoryOS.Plugins.Procurement;

/// <summary>
/// Configuration for the Procurement module. Behaviour varies by configuration, never by customer branch: a
/// factory tunes the reorder policy — how far above the reorder point to replenish, the smallest order it will
/// raise, and how requisitions are numbered.
/// </summary>
public sealed record ProcurementOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Procurement";

    /// <summary>
    /// The order-up-to level as a multiple of the reorder point: the requisition targets
    /// <c>reorderPoint × ReorderMultiple</c>. Defaults to 2 (replenish to twice the reorder point).
    /// </summary>
    public decimal ReorderMultiple { get; init; } = 2m;

    /// <summary>The smallest quantity a requisition will ever request, regardless of the computed shortfall.</summary>
    public decimal MinimumOrderQuantity { get; init; } = 1m;

    /// <summary>The prefix for generated requisition numbers.</summary>
    public string RequisitionPrefix { get; init; } = "PR";
}
