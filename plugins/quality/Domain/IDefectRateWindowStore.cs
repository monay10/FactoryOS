namespace FactoryOS.Plugins.Quality.Domain;

/// <summary>
/// Maintains a bounded rolling window of recent inspections per aggregate and returns the resulting defect-rate
/// aggregate. Tenant-scoped through the key. Replaceable by a Redis-backed store behind the interface.
/// </summary>
public interface IDefectRateWindowStore
{
    /// <summary>Folds an inspection into the aggregate's window and returns the window aggregate including it.</summary>
    /// <param name="key">The aggregate the inspection belongs to.</param>
    /// <param name="inspectedUnits">Units inspected in this batch.</param>
    /// <param name="defectiveUnits">Defective units in this batch.</param>
    /// <returns>The defect-rate aggregate over the window after folding in this inspection.</returns>
    DefectRateSnapshot Fold(QualityLineKey key, int inspectedUnits, int defectiveUnits);

    /// <summary>Returns the current rolling window of every tracked line-product aggregate for a tenant.</summary>
    /// <param name="tenant">The tenant to list within.</param>
    /// <returns>The tenant's per-line defect-rate snapshots.</returns>
    IReadOnlyCollection<QualityLineSnapshot> ForTenant(string tenant);
}
