namespace FactoryOS.Domain.Primitives;

/// <summary>
/// Non-generic base type for all domain entities. Carries the domain-event machinery so that
/// infrastructure can collect and dispatch pending events without knowing an entity's identifier type.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Gets the domain events that have been raised and are awaiting dispatch.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Clears all pending domain events, typically after they have been dispatched.</summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>Records a domain event to be dispatched when the current unit of work is saved.</summary>
    /// <param name="domainEvent">The domain event to raise.</param>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
