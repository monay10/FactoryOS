using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Warehouse.Domain;

namespace FactoryOS.Plugins.Warehouse.Application;

/// <summary>
/// The Warehouse module's consumer of <see cref="StockMovementRecorded"/>. It applies the signed delta to the
/// on-hand ledger and, when the movement crosses the item's on-hand level down to or below its reorder point,
/// raises a <see cref="LowStockDetected"/> — edge-triggered, so it does not re-alert while already low. Because a
/// movement is an increment, delivery being at-least-once, the handler deduplicates by event id before applying,
/// keeping the ledger idempotent. It references no other module — only the shared event vocabulary.
/// </summary>
public sealed class StockMovementRecordedHandler : IEventHandler<StockMovementRecorded>
{
    private readonly IEventBus _bus;
    private readonly IStockLedger _ledger;
    private readonly IProcessedEventLog _processed;
    private readonly WarehouseOptions _options;

    /// <summary>Initializes a new instance of the <see cref="StockMovementRecordedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish warehouse events on.</param>
    /// <param name="ledger">The stock ledger.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    /// <param name="options">The module options.</param>
    public StockMovementRecordedHandler(
        IEventBus bus,
        IStockLedger ledger,
        IProcessedEventLog processed,
        WarehouseOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(processed);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _ledger = ledger;
        _processed = processed;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        StockMovementRecorded integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        // At-least-once delivery: skip a movement already applied to the ledger.
        if (!_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            return;
        }

        var key = new WarehouseStockKey(integrationEvent.Tenant, integrationEvent.WarehouseId, integrationEvent.Sku);
        var change = _ledger.Apply(key, integrationEvent.QuantityDelta);

        var reorderPoint = _ledger.GetReorderPoint(key) ?? _options.DefaultReorderPoint;
        if (!LowStockEvaluator.CrossedDown(change, reorderPoint))
        {
            return;
        }

        await _bus.PublishAsync(
            new LowStockDetected
            {
                Tenant = integrationEvent.Tenant,
                WarehouseId = integrationEvent.WarehouseId,
                Sku = integrationEvent.Sku,
                OnHand = change.Current,
                ReorderPoint = reorderPoint,
                OccurredAt = integrationEvent.OccurredAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
