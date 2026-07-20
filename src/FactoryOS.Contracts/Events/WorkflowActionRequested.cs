namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a workflow rule matched a trigger and requests an action (notify, escalate, create a
/// task, …). The Notification, Scheduler and task modules consume it without referencing the Workflow module or
/// the module that raised the original trigger. It is the normalized, configuration-driven bridge from any
/// alert to any action.
/// </summary>
public sealed record WorkflowActionRequested : IntegrationEvent
{
    /// <summary>The tenant the action is for.</summary>
    public required string Tenant { get; init; }

    /// <summary>The trigger event type that matched (for example <c>SafetyStandDownTriggered</c>).</summary>
    public required string TriggerType { get; init; }

    /// <summary>A human-readable description of what triggered the action.</summary>
    public required string Subject { get; init; }

    /// <summary>The action the matched rule requests (for example <c>Notify</c> or <c>Escalate</c>).</summary>
    public required string Action { get; init; }

    /// <summary>The action priority (for example <c>Normal</c> or <c>Critical</c>).</summary>
    public required string Priority { get; init; }

    /// <summary>The channel or audience the action targets (for example <c>ops</c>).</summary>
    public required string Channel { get; init; }

    /// <summary>When the triggering event occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>The id of the trigger event that produced this action, for traceability.</summary>
    public Guid SourceEventId { get; init; }
}
