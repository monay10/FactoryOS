using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records a low-stock crossing as an activity-feed entry under the <c>Warehouse</c> category. Consumes the
/// shared <see cref="LowStockDetected"/> the Warehouse module emits, never referencing that module — the bus fans the
/// alert out to whoever cares (Procurement reorders; this feed keeps the human-readable line). Idempotent by
/// construction: the entry is keyed by the alert's event id, so at-least-once redelivery is a no-op.</summary>
public sealed class LowStockDetectedHandler : IEventHandler<LowStockDetected>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="LowStockDetectedHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public LowStockDetectedHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(LowStockDetected integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Low stock on {0} in {1}: {2:0.##} on hand at or below reorder point {3:0.##}",
            integrationEvent.Sku,
            integrationEvent.WarehouseId,
            integrationEvent.OnHand,
            integrationEvent.ReorderPoint);

        _feed.Record(new ActivityEntry(
            integrationEvent.Tenant,
            "Warehouse",
            headline,
            integrationEvent.OccurredAt,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
