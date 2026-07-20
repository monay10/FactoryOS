namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a production order reached its target quantity. Emitted exactly once per order.
/// Maintenance, scheduling, dashboards and AI agents consume it without referencing the Production module.
/// </summary>
public sealed record ProductionOrderCompleted : IntegrationEvent
{
    /// <summary>The tenant the order belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The production order that completed.</summary>
    public required string OrderId { get; init; }

    /// <summary>The product that was produced.</summary>
    public required string ProductId { get; init; }

    /// <summary>The order's target quantity.</summary>
    public int TargetQuantity { get; init; }

    /// <summary>The total units accrued against the order at completion (may exceed target if over-production is allowed).</summary>
    public int TotalProduced { get; init; }

    /// <summary>When the completing increment was reported.</summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>The id of the <see cref="ProductionCountReported"/> that carried the order over its target.</summary>
    public Guid SourceEventId { get; init; }
}
