namespace FactoryOS.Plugins.Production.Domain;

/// <summary>A read-model snapshot of a production order's progress.</summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OrderId">The order identifier.</param>
/// <param name="ProductId">The product being produced.</param>
/// <param name="TargetQuantity">The target quantity that completes the order.</param>
/// <param name="TotalProduced">The units accrued against the order so far.</param>
/// <param name="IsCompleted">Whether the order has reached its target.</param>
public sealed record ProductionOrderProgress(
    string Tenant,
    string OrderId,
    string ProductId,
    int TargetQuantity,
    int TotalProduced,
    bool IsCompleted);
