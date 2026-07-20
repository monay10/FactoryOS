using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Plugins.Workflow.Application;

/// <summary>
/// Normalizes a <see cref="PurchaseRequisitionRaised"/> — a replenishment request the Procurement module raised —
/// into a workflow signal and processes it. This gives a raised requisition a destination: the tenant's workflow
/// rule for the <c>PurchaseRequisitionRaised</c> trigger routes it to a channel (for example a buyer's desk) for
/// approval or awareness. The requisition's specifics (number, SKU, quantity, warehouse and the reason it was
/// raised) travel in the subject, without Workflow referencing Procurement.
/// </summary>
public sealed class PurchaseRequisitionRaisedHandler : IEventHandler<PurchaseRequisitionRaised>
{
    private readonly WorkflowEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="PurchaseRequisitionRaisedHandler"/> class.</summary>
    /// <param name="engine">The workflow engine.</param>
    public PurchaseRequisitionRaisedHandler(WorkflowEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc />
    public Task HandleAsync(PurchaseRequisitionRaised integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var requisition = integrationEvent.Requisition;
        var subject = string.Format(
            CultureInfo.InvariantCulture,
            "Requisition {0} raised for {1} × {2} at {3} ({4})",
            requisition.Number,
            requisition.Sku,
            requisition.RequestedQuantity,
            requisition.WarehouseId,
            integrationEvent.Reason);

        return _engine.ProcessAsync(
            new WorkflowSignal(
                requisition.Tenant,
                nameof(PurchaseRequisitionRaised),
                subject,
                integrationEvent.OccurredOnUtc,
                integrationEvent.EventId),
            cancellationToken);
    }
}
