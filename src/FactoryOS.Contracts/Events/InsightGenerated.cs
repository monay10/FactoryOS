namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that an AI agent produced an insight (an explanation, a recommended action) for a triggering
/// event, generated through the LLM Gateway. Dashboards, notification and the Company Brain consume it without
/// referencing the agent or the module that raised the trigger. AI output re-enters the system as just another
/// event on the bus — never an in-process call.
/// </summary>
public sealed record InsightGenerated : IntegrationEvent
{
    /// <summary>The tenant the insight is for.</summary>
    public required string Tenant { get; init; }

    /// <summary>The trigger event type the insight responds to (for example <c>SafetyStandDownTriggered</c>).</summary>
    public required string TriggerType { get; init; }

    /// <summary>The human-readable subject of the trigger the insight addresses.</summary>
    public required string Subject { get; init; }

    /// <summary>The AI-authored insight text.</summary>
    public required string Insight { get; init; }

    /// <summary>The upstream model that produced the insight.</summary>
    public required string Model { get; init; }

    /// <summary>When the insight was generated.</summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>The id of the trigger event this insight responds to, for traceability.</summary>
    public Guid SourceEventId { get; init; }
}
