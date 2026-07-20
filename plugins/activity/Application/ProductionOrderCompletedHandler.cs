using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records a completed production order as an activity-feed entry. Consumes the shared
/// <see cref="ProductionOrderCompleted"/> the Production module emits, never referencing that module — the bus
/// fans the fact out to whoever cares. This is the feed's first "milestone reached" line, alongside the alert-shaped
/// entries (rules, work orders, safety, quality) it already keeps.</summary>
public sealed class ProductionOrderCompletedHandler : IEventHandler<ProductionOrderCompleted>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="ProductionOrderCompletedHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public ProductionOrderCompletedHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(ProductionOrderCompleted integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Production order {0} completed: {1} — {2}/{3} units",
            integrationEvent.OrderId,
            integrationEvent.ProductId,
            integrationEvent.TotalProduced,
            integrationEvent.TargetQuantity);

        _feed.Record(new ActivityEntry(
            integrationEvent.Tenant,
            "Production",
            headline,
            integrationEvent.CompletedAt,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
