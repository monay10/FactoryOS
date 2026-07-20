namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a production order was released to the floor: which product to make and how many. Any
/// module consumes it without referencing the producer; the Production module opens progress tracking against
/// the target quantity.
/// </summary>
public sealed record ProductionOrderReleased : IntegrationEvent
{
    /// <summary>The tenant the order belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The production order identifier.</summary>
    public required string OrderId { get; init; }

    /// <summary>The product to be produced.</summary>
    public required string ProductId { get; init; }

    /// <summary>How many units the order calls for. The completion threshold.</summary>
    public int TargetQuantity { get; init; }

    /// <summary>When the order was released.</summary>
    public DateTimeOffset ReleasedAt { get; init; }
}
