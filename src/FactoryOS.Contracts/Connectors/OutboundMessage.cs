namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// A normalized message handed to an outbound connector for delivery to the outside world. It carries only the
/// Standard Model's notion of a notification — never a transport dialect — so any transport connector can
/// deliver it. Building the transport-specific payload (an HTTP body, an email envelope, …) is the connector's job.
/// </summary>
public sealed record OutboundMessage
{
    /// <summary>The tenant the message belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The logical channel the notification targeted (for example <c>ops</c>).</summary>
    public required string Channel { get; init; }

    /// <summary>The notification priority (for example <c>Normal</c> or <c>Critical</c>).</summary>
    public required string Priority { get; init; }

    /// <summary>A human-readable description of the notification.</summary>
    public required string Subject { get; init; }

    /// <summary>The action the notification fulfils (for example <c>Notify</c> or <c>Escalate</c>).</summary>
    public required string Action { get; init; }

    /// <summary>When the notification was raised.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}
