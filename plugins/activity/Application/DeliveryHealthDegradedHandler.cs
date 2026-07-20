using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records a degraded delivery transport as an activity-feed entry. Consumes the shared
/// <see cref="DeliveryHealthDegraded"/> alongside other subscribers (the bus fans out), never referencing the
/// Delivery Health module.</summary>
public sealed class DeliveryHealthDegradedHandler : IEventHandler<DeliveryHealthDegraded>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="DeliveryHealthDegradedHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public DeliveryHealthDegradedHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(DeliveryHealthDegraded integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Delivery degraded on transport '{0}' after {1} consecutive failures ({2} of {3} attempts failed)",
            integrationEvent.Transport,
            integrationEvent.ConsecutiveFailures,
            integrationEvent.Failed,
            integrationEvent.Attempts);

        _feed.Record(new ActivityEntry(
            integrationEvent.Tenant,
            "Delivery",
            headline,
            integrationEvent.DetectedAt,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
