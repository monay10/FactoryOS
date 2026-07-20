using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Contracts.Events;

/// <summary>
/// Emitted by the Maintenance module when a work order is raised. Shared vocabulary on the bus so Notification,
/// Scheduling and reporting modules react without referencing Maintenance.
/// </summary>
public sealed record WorkOrderCreated : IntegrationEvent
{
    /// <summary>The Standard Model work order that was created.</summary>
    public required WorkOrder WorkOrder { get; init; }

    /// <summary>Why the work order was raised (for example <c>EnergySpike</c>).</summary>
    public required string Reason { get; init; }

    /// <summary>The identifier of the event that triggered creation, for traceability.</summary>
    public Guid SourceEventId { get; init; }
}
