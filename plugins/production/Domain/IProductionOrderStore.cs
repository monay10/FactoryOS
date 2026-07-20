namespace FactoryOS.Plugins.Production.Domain;

/// <summary>
/// Tracks production-order progress per tenant. Registration is idempotent (a redelivered release is a no-op);
/// accrual reports whether the increment was the one that first reached the target, so completion is emitted
/// exactly once. Tenant-scoped through the key.
/// </summary>
public interface IProductionOrderStore
{
    /// <summary>Registers an order if not already known.</summary>
    /// <param name="key">The order to register.</param>
    /// <param name="productId">The product being produced.</param>
    /// <param name="targetQuantity">The target quantity that completes the order.</param>
    /// <returns><see langword="true"/> if newly registered; <see langword="false"/> if it already existed.</returns>
    bool TryRegister(ProductionOrderKey key, string productId, int targetQuantity);

    /// <summary>Accrues an increment against an order and reports the resulting progress.</summary>
    /// <param name="key">The order to accrue against.</param>
    /// <param name="producedCount">The increment of good units.</param>
    /// <returns>The accrual outcome, or <see cref="AccrualResult.NotFound"/> if the order was never released.</returns>
    AccrualResult Accrue(ProductionOrderKey key, int producedCount);

    /// <summary>Returns the progress of all orders for a tenant.</summary>
    /// <param name="tenant">The tenant to list within.</param>
    /// <returns>The tenant's order progress snapshots.</returns>
    IReadOnlyCollection<ProductionOrderProgress> ForTenant(string tenant);
}
