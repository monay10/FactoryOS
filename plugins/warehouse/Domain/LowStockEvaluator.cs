namespace FactoryOS.Plugins.Warehouse.Domain;

/// <summary>
/// Decides whether a movement crossed an item's on-hand level down to or below its reorder point. Pure and
/// deterministic — no state, no I/O — so it is fully offline-testable. It is edge-triggered: an alert fires only
/// on the crossing (from above the point to at/below it), not on every subsequent movement while already low, so
/// consumers are not flooded. A non-positive reorder point disables detection.
/// </summary>
public static class LowStockEvaluator
{
    /// <summary>Evaluates a stock change against a reorder point.</summary>
    /// <param name="change">The on-hand level before and after the movement.</param>
    /// <param name="reorderPoint">The reorder point; non-positive disables detection.</param>
    /// <returns><see langword="true"/> if this movement crossed the level down to or below the point.</returns>
    public static bool CrossedDown(StockChange change, decimal reorderPoint)
    {
        if (reorderPoint <= 0m)
        {
            return false;
        }

        return change.Previous > reorderPoint && change.Current <= reorderPoint;
    }
}
