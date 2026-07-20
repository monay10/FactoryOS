namespace FactoryOS.Plugins.Oee.Domain;

/// <summary>
/// Stores OEE snapshots per tenant, keyed by machine and period. Adding is idempotent: the first calculation
/// for a machine-period wins, so an at-least-once redelivery neither overwrites nor re-emits. Tenant-scoped.
/// </summary>
public interface IOeeStore
{
    /// <summary>Adds a snapshot if none exists yet for its machine and period.</summary>
    /// <param name="snapshot">The snapshot to add.</param>
    /// <returns><see langword="true"/> if added; <see langword="false"/> if that machine-period was already calculated.</returns>
    bool TryAdd(OeeSnapshot snapshot);

    /// <summary>Returns all snapshots for a tenant.</summary>
    /// <param name="tenant">The tenant to list within.</param>
    /// <returns>The tenant's OEE snapshots.</returns>
    IReadOnlyCollection<OeeSnapshot> ForTenant(string tenant);
}
