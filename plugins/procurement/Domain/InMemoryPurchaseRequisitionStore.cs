using System.Collections.Concurrent;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Plugins.Procurement.Domain;

/// <summary>
/// The default in-memory <see cref="IPurchaseRequisitionStore"/>: a per-tenant map of requisitions keyed by
/// number. Each tenant has its own bucket, so no tenant can read or overwrite another's. Replaceable by an
/// EF Core-backed store behind the interface.
/// </summary>
public sealed class InMemoryPurchaseRequisitionStore : IPurchaseRequisitionStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PurchaseRequisition>> _byTenant =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryAdd(PurchaseRequisition requisition)
    {
        ArgumentNullException.ThrowIfNull(requisition);
        var bucket = _byTenant.GetOrAdd(requisition.Tenant, static _ => new ConcurrentDictionary<string, PurchaseRequisition>(StringComparer.Ordinal));
        return bucket.TryAdd(requisition.Number, requisition);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PurchaseRequisition> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _byTenant.TryGetValue(tenant, out var bucket) ? bucket.Values.ToList() : [];
    }
}
