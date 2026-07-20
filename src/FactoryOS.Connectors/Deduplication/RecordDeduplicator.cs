using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Deduplication;

/// <summary>
/// Default <see cref="IRecordDeduplicator"/>. Collapses records that share a (tenant, entity type,
/// natural key) identity, keeping the last value seen — a later read wins — while preserving the order
/// in which each key first appeared for stable, per-aggregate output.
/// </summary>
public sealed class RecordDeduplicator : IRecordDeduplicator
{
    // A unit-separator delimiter keeps the composite key unambiguous across component boundaries.
    private const char KeySeparator = '';

    /// <inheritdoc />
    public IReadOnlyList<NormalizedRecord> Deduplicate(IEnumerable<NormalizedRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var order = new List<string>();
        var latest = new Dictionary<string, NormalizedRecord>(StringComparer.Ordinal);

        foreach (var record in records)
        {
            var key = string.Join(KeySeparator, record.Tenant, record.EntityType, record.NaturalKey);
            if (!latest.ContainsKey(key))
            {
                order.Add(key);
            }

            latest[key] = record;
        }

        var result = new NormalizedRecord[order.Count];
        for (var index = 0; index < order.Count; index++)
        {
            result[index] = latest[order[index]];
        }

        return result;
    }
}
