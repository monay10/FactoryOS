namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a declarative rule matched a Standard Model observation — a configured threshold on a
/// metric was crossed. It belongs to the shared vocabulary: the Rule Engine emits it and any module, connector
/// or AI agent (Notification, Workflow, Maintenance, …) consumes it to act, without referencing the Rule Engine.
/// The rule that fired is data, never core code, so a factory tunes its automation purely by configuration.
/// Delivery is at-least-once; consumers deduplicate by <see cref="IIntegrationEvent.EventId"/>.
/// </summary>
public sealed record RuleTriggered : IntegrationEvent
{
    /// <summary>The tenant the rule belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The rule's stable id (for example <c>overtemp-press-1</c>).</summary>
    public required string RuleId { get; init; }

    /// <summary>The metric the rule watches (for example <c>Temperature</c>).</summary>
    public required string Metric { get; init; }

    /// <summary>The meter or sensor whose reading matched.</summary>
    public required string MeterId { get; init; }

    /// <summary>The observed value that matched the rule.</summary>
    public decimal Value { get; init; }

    /// <summary>The comparison the rule applied, as its canonical name (for example <c>GreaterThan</c>).</summary>
    public required string Operator { get; init; }

    /// <summary>The threshold the value was compared against.</summary>
    public decimal Threshold { get; init; }

    /// <summary>The action the rule requests (for example <c>RaiseMaintenanceAlert</c>).</summary>
    public required string Action { get; init; }

    /// <summary>The instant the matching reading was taken.</summary>
    public DateTimeOffset TriggeredAt { get; init; }

    /// <summary>The id of the reading event that triggered the rule, for idempotent consumers.</summary>
    public Guid SourceEventId { get; init; }
}
