namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a transport's notification delivery has degraded — its consecutive-failure streak reached
/// the configured threshold. The Delivery Health module raises it once per crossing (a subsequent success resets
/// the streak); the timeline, notification and AI layers consume it to surface the degradation without referencing
/// the Delivery Health module or the connectors.
/// </summary>
public sealed record DeliveryHealthDegraded : IntegrationEvent
{
    /// <summary>The tenant the degradation is for.</summary>
    public required string Tenant { get; init; }

    /// <summary>The transport whose delivery degraded (for example <c>webhook</c>).</summary>
    public required string Transport { get; init; }

    /// <summary>The consecutive-failure streak that triggered the alert (equal to the configured threshold).</summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>The total delivery attempts recorded for the transport.</summary>
    public int Attempts { get; init; }

    /// <summary>The total failed deliveries recorded for the transport.</summary>
    public int Failed { get; init; }

    /// <summary>The most recent failure detail from the connector, if any.</summary>
    public string? LastDetail { get; init; }

    /// <summary>When the degradation was detected (the triggering delivery event's instant).</summary>
    public DateTimeOffset DetectedAt { get; init; }

    /// <summary>The id of the <see cref="NotificationDelivered"/> that pushed the streak over the threshold.</summary>
    public Guid SourceEventId { get; init; }
}
