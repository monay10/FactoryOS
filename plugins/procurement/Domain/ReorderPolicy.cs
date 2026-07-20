namespace FactoryOS.Plugins.Procurement.Domain;

/// <summary>
/// Decides how much to requisition when an item goes low. Pure and deterministic — no state, no I/O — so it is
/// fully offline-testable. The policy replenishes up to <c>reorderPoint × ReorderMultiple</c>, never ordering
/// less than the configured minimum.
/// </summary>
public static class ReorderPolicy
{
    /// <summary>Computes the quantity to requisition for a low-stock item.</summary>
    /// <param name="onHand">The current on-hand quantity.</param>
    /// <param name="reorderPoint">The reorder point that was crossed.</param>
    /// <param name="options">The reorder-multiple and minimum-order settings.</param>
    /// <returns>The quantity to request, at least the configured minimum order quantity.</returns>
    public static decimal RequestedQuantity(decimal onHand, decimal reorderPoint, ProcurementOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var target = reorderPoint * options.ReorderMultiple;
        var shortfall = target - onHand;
        return Math.Max(shortfall, options.MinimumOrderQuantity);
    }
}
