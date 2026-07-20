using FactoryOS.Agents.Insight.Domain;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Insight.Application;

/// <summary>
/// Folds an emitted <see cref="InsightGenerated"/> into the tenant's insight feed read model. The agent reads its
/// own output back off the bus — never through an in-process call into the reasoning path — so the feed is a plain
/// projection, decoupled from generation. Idempotent: redelivery of the same insight is dropped by the feed.
/// </summary>
public sealed class InsightGeneratedHandler : IEventHandler<InsightGenerated>
{
    private readonly IInsightFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="InsightGeneratedHandler"/> class.</summary>
    /// <param name="feed">The insight feed read model.</param>
    public InsightGeneratedHandler(IInsightFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(InsightGenerated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        _feed.TryRecord(
            integrationEvent.Tenant,
            new InsightRecord(
                integrationEvent.EventId,
                integrationEvent.SourceEventId,
                integrationEvent.TriggerType,
                integrationEvent.Subject,
                integrationEvent.Insight,
                integrationEvent.Model,
                integrationEvent.GeneratedAt));

        return Task.CompletedTask;
    }
}
