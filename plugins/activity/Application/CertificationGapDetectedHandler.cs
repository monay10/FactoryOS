using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records a certification gap as an activity-feed entry under the <c>Compliance</c> category. Consumes the
/// shared <see cref="CertificationGapDetected"/> the HR module emits, never referencing that module — the bus fans the
/// alert out to whoever cares (Notification pages a supervisor; this feed keeps the human-readable line). Idempotent
/// by construction: the entry is keyed by the alert's event id, so at-least-once redelivery is a no-op.</summary>
public sealed class CertificationGapDetectedHandler : IEventHandler<CertificationGapDetected>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="CertificationGapDetectedHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public CertificationGapDetectedHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(CertificationGapDetected integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Certification gap on shift {0}: {1} staffed without {2} ({3})",
            integrationEvent.ShiftId,
            integrationEvent.WorkerId,
            integrationEvent.RequiredCertification,
            integrationEvent.Reason.ToUpperInvariant());

        _feed.Record(new ActivityEntry(
            integrationEvent.Tenant,
            "Compliance",
            headline,
            integrationEvent.ShiftStart,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
