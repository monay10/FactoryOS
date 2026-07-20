using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Plugins.Procurement.Domain;

/// <summary>
/// Stores purchase requisitions per tenant. Adding is idempotent by requisition number, which both persists the
/// requisition and guards against raising it twice for the same trigger. Tenant-scoped by construction.
/// </summary>
public interface IPurchaseRequisitionStore
{
    /// <summary>Adds a requisition if its number is not already present for the tenant.</summary>
    /// <param name="requisition">The requisition to add.</param>
    /// <returns><see langword="true"/> if added; <see langword="false"/> if the number already existed.</returns>
    bool TryAdd(PurchaseRequisition requisition);

    /// <summary>Returns all requisitions for a tenant.</summary>
    /// <param name="tenant">The tenant to list within.</param>
    /// <returns>The tenant's requisitions.</returns>
    IReadOnlyCollection<PurchaseRequisition> ForTenant(string tenant);
}
