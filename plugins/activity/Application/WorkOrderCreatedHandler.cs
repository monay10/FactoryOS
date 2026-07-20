using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records a raised work order as an activity-feed entry. Consumes the shared <see cref="WorkOrderCreated"/>
/// alongside the Workflow module (the bus fans out), never referencing the Maintenance module.</summary>
public sealed class WorkOrderCreatedHandler : IEventHandler<WorkOrderCreated>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="WorkOrderCreatedHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public WorkOrderCreatedHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(WorkOrderCreated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var workOrder = integrationEvent.WorkOrder;
        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Work order {0} raised on {1}: {2} ({3})",
            workOrder.Number,
            workOrder.AssetCode,
            workOrder.Title,
            integrationEvent.Reason);

        _feed.Record(new ActivityEntry(
            workOrder.Tenant,
            "Maintenance",
            headline,
            integrationEvent.OccurredOnUtc,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
