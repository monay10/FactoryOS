namespace FactoryOS.Plugins.Workflow.Domain;

/// <summary>
/// The normalized form of any triggering alert the Workflow module reacts to. Each handler maps its specific
/// integration event into this shape, so the engine matches rules against one uniform structure regardless of
/// which module raised the trigger.
/// </summary>
/// <param name="Tenant">The tenant the trigger belongs to.</param>
/// <param name="TriggerType">The trigger event type name (for example <c>QualityAlertRaised</c>).</param>
/// <param name="Subject">A human-readable description of the trigger.</param>
/// <param name="OccurredAt">When the trigger occurred.</param>
/// <param name="SourceEventId">The triggering event's id, for idempotency and traceability.</param>
public readonly record struct WorkflowSignal(
    string Tenant,
    string TriggerType,
    string Subject,
    DateTimeOffset OccurredAt,
    Guid SourceEventId);
