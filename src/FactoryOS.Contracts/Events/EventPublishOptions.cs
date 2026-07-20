namespace FactoryOS.Contracts.Events;

/// <summary>Optional metadata supplied when publishing an event.</summary>
public sealed record EventPublishOptions
{
    /// <summary>Gets the shared default options (normal priority, generated correlation).</summary>
    public static EventPublishOptions Default { get; } = new();

    /// <summary>Gets the priority to publish the event with. Defaults to <see cref="EventPriority.Normal"/>.</summary>
    public EventPriority Priority { get; init; } = EventPriority.Normal;

    /// <summary>Gets the correlation identifier to reuse, or <see langword="null"/> to generate a new one.</summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>Gets the identifier of the message that caused this publish, when applicable.</summary>
    public Guid? CausationId { get; init; }
}
