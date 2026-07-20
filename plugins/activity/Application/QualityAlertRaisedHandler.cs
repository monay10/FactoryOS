using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records a quality alert as an activity-feed entry. Consumes the shared <see cref="QualityAlertRaised"/>,
/// never referencing the Quality module.</summary>
public sealed class QualityAlertRaisedHandler : IEventHandler<QualityAlertRaised>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="QualityAlertRaisedHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public QualityAlertRaisedHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(QualityAlertRaised integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Quality alert on {0}/{1}: defect rate {2:P1} over threshold {3:P1}",
            integrationEvent.LineId,
            integrationEvent.ProductId,
            integrationEvent.DefectRate,
            integrationEvent.Threshold);

        _feed.Record(new ActivityEntry(
            integrationEvent.Tenant,
            "Quality",
            headline,
            integrationEvent.InspectedAt,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
