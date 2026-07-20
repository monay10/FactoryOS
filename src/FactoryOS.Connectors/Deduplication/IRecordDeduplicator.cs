using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Deduplication;

/// <summary>
/// Removes duplicate normalized records. Event delivery is at-least-once and ERP reads can repeat a row,
/// so consumers deduplicate by natural key. Ordering is preserved per aggregate (natural key), not
/// globally.
/// </summary>
public interface IRecordDeduplicator
{
    /// <summary>Deduplicates a sequence of normalized records by tenant, entity type and natural key.</summary>
    /// <param name="records">The records to deduplicate.</param>
    /// <returns>The distinct records, keeping the last value seen for each key in first-seen order.</returns>
    IReadOnlyList<NormalizedRecord> Deduplicate(IEnumerable<NormalizedRecord> records);
}
