using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Production.Domain;

namespace FactoryOS.Plugins.Production.Application;

/// <summary>
/// The Production module's consumer of <see cref="ProductionCountReported"/>. It accrues the increment against
/// the order's progress and, when an accrual first carries the order to its target, publishes a
/// <see cref="ProductionOrderCompleted"/> — exactly once. Because counts are increments, delivery being
/// at-least-once, the handler deduplicates by event id before accruing so a redelivery is never double-counted.
/// Counts for an order that was never released are ignored (per-aggregate ordering releases before counting).
/// </summary>
public sealed class ProductionCountReportedHandler : IEventHandler<ProductionCountReported>
{
    private readonly IEventBus _bus;
    private readonly IProductionOrderStore _store;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="ProductionCountReportedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish production events on.</param>
    /// <param name="store">The production-order progress store.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public ProductionCountReportedHandler(IEventBus bus, IProductionOrderStore store, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(processed);
        _bus = bus;
        _store = store;
        _processed = processed;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        ProductionCountReported integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        // At-least-once delivery: skip an increment already accrued.
        if (!_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            return;
        }

        var key = new ProductionOrderKey(integrationEvent.Tenant, integrationEvent.OrderId);
        var result = _store.Accrue(key, integrationEvent.ProducedCount);
        if (!result.JustCompleted)
        {
            return; // unknown order, or target not yet reached, or already completed
        }

        await _bus.PublishAsync(
            new ProductionOrderCompleted
            {
                Tenant = integrationEvent.Tenant,
                OrderId = integrationEvent.OrderId,
                ProductId = result.ProductId,
                TargetQuantity = result.TargetQuantity,
                TotalProduced = result.TotalProduced,
                CompletedAt = integrationEvent.ReportedAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
