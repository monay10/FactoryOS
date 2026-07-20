namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a batch of units was produced against an order — an increment, not a running total.
/// The Production module accrues it into the order's progress; because it is an increment, consumers must
/// deduplicate by event id so an at-least-once redelivery is never double-counted.
/// </summary>
public sealed record ProductionCountReported : IntegrationEvent
{
    /// <summary>The tenant the order belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The production order the units were made against.</summary>
    public required string OrderId { get; init; }

    /// <summary>How many good units were produced in this increment.</summary>
    public int ProducedCount { get; init; }

    /// <summary>When the units were reported.</summary>
    public DateTimeOffset ReportedAt { get; init; }
}
