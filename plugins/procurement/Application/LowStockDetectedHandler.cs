using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Procurement.Domain;

namespace FactoryOS.Plugins.Procurement.Application;

/// <summary>
/// The Procurement module's consumer of <see cref="LowStockDetected"/>. It raises a purchase requisition to
/// replenish the low item and announces it with <see cref="PurchaseRequisitionRaised"/>. It references no other
/// module — only the shared event vocabulary (the alert comes from Warehouse purely over the bus). Because the
/// requisition number is derived from the alert's event id and the store adds by number, redelivery of the same
/// alert neither raises a second requisition nor re-publishes.
/// </summary>
public sealed class LowStockDetectedHandler : IEventHandler<LowStockDetected>
{
    private readonly IEventBus _bus;
    private readonly IPurchaseRequisitionStore _store;
    private readonly ProcurementOptions _options;

    /// <summary>Initializes a new instance of the <see cref="LowStockDetectedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish procurement events on.</param>
    /// <param name="store">The requisition store.</param>
    /// <param name="options">The module options.</param>
    public LowStockDetectedHandler(IEventBus bus, IPurchaseRequisitionStore store, ProcurementOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _store = store;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        LowStockDetected integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var requisition = LowStockRequisitionFactory.FromLowStock(integrationEvent, _options);

        // Idempotent: the number is deterministic per alert, so a duplicate add is a no-op and we do not re-announce.
        if (!_store.TryAdd(requisition))
        {
            return;
        }

        await _bus.PublishAsync(
            new PurchaseRequisitionRaised
            {
                Requisition = requisition,
                Reason = "LowStock",
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
