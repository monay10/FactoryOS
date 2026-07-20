using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Warehouse.Domain;

namespace FactoryOS.Plugins.Warehouse.Application;

/// <summary>
/// The Warehouse module's consumer of <see cref="ItemReorderPointDefined"/>. It records the reorder point as the
/// item's low-stock threshold. Setting is last-write-wins, so a redelivery is harmless. It references no other
/// module — only the shared event vocabulary.
/// </summary>
public sealed class ItemReorderPointDefinedHandler : IEventHandler<ItemReorderPointDefined>
{
    private readonly IStockLedger _ledger;

    /// <summary>Initializes a new instance of the <see cref="ItemReorderPointDefinedHandler"/> class.</summary>
    /// <param name="ledger">The stock ledger.</param>
    public ItemReorderPointDefinedHandler(IStockLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        _ledger = ledger;
    }

    /// <inheritdoc />
    public Task HandleAsync(
        ItemReorderPointDefined integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        _ledger.SetReorderPoint(
            new WarehouseStockKey(integrationEvent.Tenant, integrationEvent.WarehouseId, integrationEvent.Sku),
            integrationEvent.ReorderPoint);

        return Task.CompletedTask;
    }
}
