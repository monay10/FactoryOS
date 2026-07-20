namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a notification was dispatched for a workflow action — routed to a transport (email, SMS,
/// chat, …) by the tenant's channel configuration. The Notification module records the intent and emits this;
/// the actual delivery is a connector's job (connectors are the only door to the outside), so a transport
/// connector consumes this without referencing the Notification module.
/// </summary>
public sealed record NotificationDispatched : IntegrationEvent
{
    /// <summary>The tenant the notification is for.</summary>
    public required string Tenant { get; init; }

    /// <summary>The logical channel the action targeted (for example <c>ops</c> or <c>procurement</c>).</summary>
    public required string Channel { get; init; }

    /// <summary>The transport the channel routed to (for example <c>email</c>, <c>sms</c> or <c>log</c>).</summary>
    public required string Transport { get; init; }

    /// <summary>The notification priority carried over from the action (for example <c>Normal</c> or <c>Critical</c>).</summary>
    public required string Priority { get; init; }

    /// <summary>A human-readable description of what the notification is about.</summary>
    public required string Subject { get; init; }

    /// <summary>The action the notification fulfils (for example <c>Notify</c> or <c>Escalate</c>).</summary>
    public required string Action { get; init; }

    /// <summary>When the notification was dispatched.</summary>
    public DateTimeOffset DispatchedAt { get; init; }

    /// <summary>The id of the <see cref="WorkflowActionRequested"/> that produced this notification.</summary>
    public Guid SourceEventId { get; init; }
}
