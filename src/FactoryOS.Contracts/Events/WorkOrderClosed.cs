using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Contracts.Events;

/// <summary>
/// Emitted by the Maintenance module when a work order is closed. Shared vocabulary on the bus so Notification,
/// reporting and dashboard modules react without referencing Maintenance. Publication is idempotent: a work order is
/// announced closed only on the transition, so redelivering a close command neither re-closes nor re-publishes.
/// </summary>
public sealed record WorkOrderClosed : IntegrationEvent
{
    /// <summary>The Standard Model work order in its closed state.</summary>
    public required WorkOrder WorkOrder { get; init; }

    /// <summary>Who closed the work order (a user name or system actor), when known.</summary>
    public string? ClosedBy { get; init; }
}
