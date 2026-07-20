using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Oee.Domain;

/// <summary>
/// The default in-memory <see cref="IOeeStore"/>: a per-tenant map of snapshots keyed by machine and period.
/// Each tenant has its own bucket, so no tenant reads or overwrites another's. Replaceable by an EF Core or
/// Redis read-model behind the interface.
/// </summary>
public sealed class InMemoryOeeStore : IOeeStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<(string MachineId, DateTimeOffset PeriodEnd), OeeSnapshot>> _byTenant =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryAdd(OeeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var bucket = _byTenant.GetOrAdd(snapshot.Tenant, static _ => new());
        return bucket.TryAdd((snapshot.MachineId, snapshot.PeriodEnd), snapshot);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<OeeSnapshot> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _byTenant.TryGetValue(tenant, out var bucket) ? bucket.Values.ToList() : [];
    }
}
