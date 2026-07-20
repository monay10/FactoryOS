using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Notification.Domain;

/// <summary>
/// The default in-memory <see cref="INotificationOutbox"/>. State is partitioned by tenant — no code path
/// crosses tenants — and each tenant's outbox is guarded by its own lock. Idempotency is enforced per tenant by
/// the set of source event ids already recorded, so at-least-once delivery never doubles a notification.
/// </summary>
public sealed class InMemoryNotificationOutbox : INotificationOutbox
{
    private sealed class TenantOutbox
    {
        public Lock Gate { get; } = new();

        public HashSet<Guid> Seen { get; } = [];

        public List<NotificationRecord> Records { get; } = [];
    }

    private readonly ConcurrentDictionary<string, TenantOutbox> _outboxes = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryRecord(string tenant, Guid sourceEventId, NotificationRecord record)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var outbox = _outboxes.GetOrAdd(tenant, static _ => new TenantOutbox());
        lock (outbox.Gate)
        {
            if (!outbox.Seen.Add(sourceEventId))
            {
                return false;
            }

            outbox.Records.Add(record);
            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<NotificationRecord> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        if (!_outboxes.TryGetValue(tenant, out var outbox))
        {
            return [];
        }

        lock (outbox.Gate)
        {
            var ordered = new NotificationRecord[outbox.Records.Count];
            var index = ordered.Length - 1;
            foreach (var record in outbox.Records)
            {
                ordered[index--] = record; // stored oldest-to-newest; hand back newest first
            }

            return ordered;
        }
    }
}
