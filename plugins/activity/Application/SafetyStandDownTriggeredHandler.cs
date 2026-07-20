using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records a safety stand-down as an activity-feed entry. Consumes the shared
/// <see cref="SafetyStandDownTriggered"/>, never referencing the Safety module.</summary>
public sealed class SafetyStandDownTriggeredHandler : IEventHandler<SafetyStandDownTriggered>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="SafetyStandDownTriggeredHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public SafetyStandDownTriggeredHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(SafetyStandDownTriggered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Safety stand-down at {0} ({1}, {2} incidents in window)",
            integrationEvent.SiteId,
            integrationEvent.Reason,
            integrationEvent.WindowIncidentCount);

        _feed.Record(new ActivityEntry(
            integrationEvent.Tenant,
            "Safety",
            headline,
            integrationEvent.OccurredAt,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
