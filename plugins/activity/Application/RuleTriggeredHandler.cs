using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Plugins.Activity.Application;

/// <summary>Records a fired rule as an activity-feed entry. Consumes the shared <see cref="RuleTriggered"/>
/// alongside other subscribers (the bus fans out), never referencing the Rule Engine module.</summary>
public sealed class RuleTriggeredHandler : IEventHandler<RuleTriggered>
{
    private readonly IActivityFeed _feed;

    /// <summary>Initializes a new instance of the <see cref="RuleTriggeredHandler"/> class.</summary>
    /// <param name="feed">The activity feed read-model.</param>
    public RuleTriggeredHandler(IActivityFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
    }

    /// <inheritdoc />
    public Task HandleAsync(RuleTriggered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var headline = string.Format(
            CultureInfo.InvariantCulture,
            "Rule {0} fired on {1} ({2} {3:0.#} {4} {5:0.#}) → {6}",
            integrationEvent.RuleId,
            integrationEvent.MeterId,
            integrationEvent.Metric,
            integrationEvent.Value,
            integrationEvent.Operator,
            integrationEvent.Threshold,
            integrationEvent.Action);

        _feed.Record(new ActivityEntry(
            integrationEvent.Tenant,
            "Rule",
            headline,
            integrationEvent.TriggeredAt,
            integrationEvent.EventId));

        return Task.CompletedTask;
    }
}
