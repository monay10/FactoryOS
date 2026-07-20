using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records an energy spike as an activity-feed entry under the <c>Energy</c> category. Consumes the shared
/// <see cref="EnergySpikeDetected"/> the Energy module emits, never referencing that module — the bus fans the alert
/// out to whoever cares (Maintenance raises a work order; this feed keeps the human-readable line). Idempotent by
/// construction: the entry is keyed by the spike's event id, so redelivery is a no-op.</summary>
public sealed class EnergySpikeDetectedHandler : IEventHandler<EnergySpikeDetected>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="EnergySpikeDetectedHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public EnergySpikeDetectedHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(EnergySpikeDetected integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Energy spike on {0}: {1} at {2:0.##}{3} is {4:0.#}% above baseline {5:0.##}{3}",
            integrationEvent.MeterId,
            integrationEvent.Metric,
            integrationEvent.Value,
            integrationEvent.Unit,
            integrationEvent.DeltaPercent,
            integrationEvent.Baseline);

        _feed.Record(new ActivityEntry(
            integrationEvent.Tenant,
            "Energy",
            headline,
            integrationEvent.ReadingAt,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
