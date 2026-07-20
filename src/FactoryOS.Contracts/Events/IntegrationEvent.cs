namespace FactoryOS.Contracts.Events;

/// <summary>
/// Base record for integration events, supplying a time-ordered identity and an occurrence
/// timestamp so derived events only declare their own payload.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    /// <summary>Initializes a new instance of the <see cref="IntegrationEvent"/> record.</summary>
    protected IntegrationEvent()
    {
        EventId = Guid.CreateVersion7();
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public Guid EventId { get; init; }

    /// <inheritdoc />
    public DateTimeOffset OccurredOnUtc { get; init; }
}
