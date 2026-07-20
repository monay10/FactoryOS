namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that an outbound connector attempted delivery of a dispatched notification, and whether it
/// succeeded. It closes the notification audit trail — dispatched, then delivered (or not) — and lets reporting
/// and AI agents track delivery health without referencing the connector or the Notification module.
/// </summary>
public sealed record NotificationDelivered : IntegrationEvent
{
    /// <summary>The tenant the notification belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The transport the delivery was attempted on (for example <c>webhook</c> or <c>log</c>).</summary>
    public required string Transport { get; init; }

    /// <summary>The logical channel the notification targeted.</summary>
    public required string Channel { get; init; }

    /// <summary>A human-readable description of the notification.</summary>
    public required string Subject { get; init; }

    /// <summary>Whether the delivery succeeded.</summary>
    public bool Delivered { get; init; }

    /// <summary>An optional detail about the outcome (a provider message id, an error).</summary>
    public string? Detail { get; init; }

    /// <summary>When the delivery was attempted.</summary>
    public DateTimeOffset DeliveredAt { get; init; }

    /// <summary>The id of the <see cref="NotificationDispatched"/> this delivery fulfils.</summary>
    public Guid SourceEventId { get; init; }
}
