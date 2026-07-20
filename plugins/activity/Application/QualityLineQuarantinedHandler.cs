using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records a quarantined line as an activity-feed entry, so the factory timeline shows manual quality holds
/// alongside automatic alerts. Consumes the shared <see cref="QualityLineQuarantined"/> the Quality module publishes,
/// never referencing that module. Recording is idempotent by the event id, so redelivery never doubles the entry.</summary>
public sealed class QualityLineQuarantinedHandler : IEventHandler<QualityLineQuarantined>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="QualityLineQuarantinedHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public QualityLineQuarantinedHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(QualityLineQuarantined integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var by = string.IsNullOrWhiteSpace(integrationEvent.QuarantinedBy) ? "the system" : integrationEvent.QuarantinedBy;
        var reason = string.IsNullOrWhiteSpace(integrationEvent.Reason) ? string.Empty : $" ({integrationEvent.Reason})";
        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Line {0} quarantined by {1}{2}",
            integrationEvent.LineId,
            by,
            reason);

        _feed.Record(new ActivityEntry(
            integrationEvent.Tenant,
            "Quality",
            headline,
            integrationEvent.OccurredOnUtc,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
