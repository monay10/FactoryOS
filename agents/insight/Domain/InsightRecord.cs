namespace FactoryOS.Agents.Insight.Domain;

/// <summary>
/// One generated insight, flattened for the read side: the AI-authored text plus the trigger it addresses and the
/// model that produced it. The feed is a projection of the <c>InsightGenerated</c> facts the agent emits, so a
/// screen or another agent can read the tenant's recent AI reasoning without replaying the bus.
/// </summary>
/// <param name="EventId">The <c>InsightGenerated</c> event id — the feed's dedup key across redeliveries.</param>
/// <param name="SourceEventId">The id of the trigger event the insight responds to, for traceability.</param>
/// <param name="TriggerType">The trigger event type (for example <c>RuleTriggered</c>).</param>
/// <param name="Subject">The human-readable subject of the trigger the insight addresses.</param>
/// <param name="Insight">The AI-authored insight text.</param>
/// <param name="Model">The upstream model that produced the insight.</param>
/// <param name="GeneratedAt">When the insight was generated.</param>
public readonly record struct InsightRecord(
    Guid EventId,
    Guid SourceEventId,
    string TriggerType,
    string Subject,
    string Insight,
    string Model,
    DateTimeOffset GeneratedAt);
