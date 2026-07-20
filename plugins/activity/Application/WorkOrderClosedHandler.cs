using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records a closed work order as an activity-feed entry, so the factory timeline shows completion alongside
/// the raising of work. Consumes the shared <see cref="WorkOrderClosed"/> the Maintenance module publishes, never
/// referencing that module. Recording is idempotent by the event id, so redelivery never doubles the entry.</summary>
public sealed class WorkOrderClosedHandler : IEventHandler<WorkOrderClosed>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="WorkOrderClosedHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public WorkOrderClosedHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(WorkOrderClosed integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var workOrder = integrationEvent.WorkOrder;
        var by = string.IsNullOrWhiteSpace(integrationEvent.ClosedBy) ? "the system" : integrationEvent.ClosedBy;
        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Work order {0} closed by {1}: {2}",
            workOrder.Number,
            by,
            workOrder.Title);

        _feed.Record(new ActivityEntry(
            workOrder.Tenant,
            "Maintenance",
            headline,
            integrationEvent.OccurredOnUtc,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
