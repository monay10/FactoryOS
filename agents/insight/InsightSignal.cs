namespace FactoryOS.Agents.Insight;

/// <summary>
/// The normalized "something happened worth explaining" input the agent reasons over. Each specific alert
/// handler maps its event into this one uniform shape, so the agent's reasoning path is single and generic.
/// </summary>
/// <param name="Tenant">The tenant the trigger belongs to.</param>
/// <param name="TriggerType">The trigger event type (for example <c>SafetyStandDownTriggered</c>).</param>
/// <param name="Subject">A human-readable description of what happened.</param>
/// <param name="OccurredAt">When the triggering event occurred.</param>
/// <param name="SourceEventId">The triggering event's id, for idempotency and traceability.</param>
public readonly record struct InsightSignal(
    string Tenant,
    string TriggerType,
    string Subject,
    DateTimeOffset OccurredAt,
    Guid SourceEventId);
