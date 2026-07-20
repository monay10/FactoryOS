using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records an AI-generated insight as an activity-feed entry under the <c>Insight</c> category. Consumes the
/// shared <see cref="InsightGenerated"/> the Insight agent re-enters on the bus, never referencing that agent — the
/// bus fans the AI output out to whoever cares, so an operator sees the root-cause hypothesis on the human timeline
/// alongside the raw alerts. Idempotent by construction: the entry is keyed by the insight's own event id, so
/// at-least-once redelivery is a no-op.</summary>
public sealed class InsightGeneratedHandler : IEventHandler<InsightGenerated>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="InsightGeneratedHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public InsightGeneratedHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(InsightGenerated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "AI insight on {0} ({1}): {2}",
            integrationEvent.Subject,
            integrationEvent.TriggerType,
            integrationEvent.Insight);

        _feed.Record(new ActivityEntry(
            integrationEvent.Tenant,
            "Insight",
            headline,
            integrationEvent.GeneratedAt,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
