using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a purchase requisition was raised to replenish an item. Emitted by the Procurement
/// module; Notification, approval workflows, ERP connectors and dashboards react without referencing Procurement.
/// </summary>
public sealed record PurchaseRequisitionRaised : IntegrationEvent
{
    /// <summary>The Standard Model purchase requisition that was raised.</summary>
    public required PurchaseRequisition Requisition { get; init; }

    /// <summary>Why the requisition was raised (for example <c>LowStock</c>).</summary>
    public required string Reason { get; init; }

    /// <summary>The identifier of the event that triggered the requisition, for traceability.</summary>
    public Guid SourceEventId { get; init; }
}
