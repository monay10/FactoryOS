using System.Collections.Concurrent;

namespace FactoryOS.Connectors.Log.Domain;

/// <summary>
/// The default in-memory <see cref="IDeliveryJournal"/>. Partitioned by tenant — no code path crosses tenants —
/// and each tenant's journal is guarded by its own lock.
/// </summary>
public sealed class InMemoryDeliveryJournal : IDeliveryJournal
{
    private sealed class TenantJournal
    {
        public Lock Gate { get; } = new();

        public List<DeliveryRecord> Records { get; } = [];
    }

    private readonly ConcurrentDictionary<string, TenantJournal> _journals = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Record(string tenant, DeliveryRecord record)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var journal = _journals.GetOrAdd(tenant, static _ => new TenantJournal());
        lock (journal.Gate)
        {
            journal.Records.Add(record);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DeliveryRecord> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        if (!_journals.TryGetValue(tenant, out var journal))
        {
            return [];
        }

        lock (journal.Gate)
        {
            var ordered = new DeliveryRecord[journal.Records.Count];
            var index = ordered.Length - 1;
            foreach (var record in journal.Records)
            {
                ordered[index--] = record; // stored oldest-to-newest; hand back newest first
            }

            return ordered;
        }
    }
}
