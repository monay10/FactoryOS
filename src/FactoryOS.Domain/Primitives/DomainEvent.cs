namespace FactoryOS.Domain.Primitives;

/// <summary>
/// Base record for domain events. Supplies a unique identity and an occurrence timestamp so that
/// derived events only need to declare their own payload.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <summary>Initializes a new instance of the <see cref="DomainEvent"/> record.</summary>
    protected DomainEvent()
    {
        EventId = Guid.CreateVersion7();
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public Guid EventId { get; init; }

    /// <inheritdoc />
    public DateTimeOffset OccurredOnUtc { get; init; }
}
