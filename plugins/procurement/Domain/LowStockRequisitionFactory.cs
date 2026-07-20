using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Plugins.Procurement.Domain;

/// <summary>
/// Builds the <see cref="PurchaseRequisition"/> raised in response to a low-stock alert. Pure and deterministic:
/// the requisition number is derived from the triggering event's id, so re-processing the same alert yields the
/// same number — the basis for idempotent, duplicate-free requisitioning.
/// </summary>
public static class LowStockRequisitionFactory
{
    /// <summary>Creates the requisition for a low-stock alert under the given options.</summary>
    /// <param name="alert">The triggering low-stock event.</param>
    /// <param name="options">The reorder policy and numbering options.</param>
    /// <returns>A <c>Draft</c> requisition for the low item, sized by the reorder policy.</returns>
    public static PurchaseRequisition FromLowStock(LowStockDetected alert, ProcurementOptions options)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ArgumentNullException.ThrowIfNull(options);

        var number = $"{options.RequisitionPrefix}-{alert.EventId:N}"[..(options.RequisitionPrefix.Length + 9)]
            .ToUpperInvariant();

        return new PurchaseRequisition
        {
            Tenant = alert.Tenant,
            Number = number,
            Sku = alert.Sku,
            WarehouseId = alert.WarehouseId,
            RequestedQuantity = ReorderPolicy.RequestedQuantity(alert.OnHand, alert.ReorderPoint, options),
            Status = "Draft",
        };
    }
}
