using FactoryOS.Domain.Primitives;

namespace FactoryOS.Tests.Primitives;

public sealed class AggregateRootDomainEventsTests
{
    private sealed record SomethingHappened(string Detail) : DomainEvent;

    private sealed class Order : AggregateRoot<Guid>
    {
        public Order(Guid id)
            : base(id)
        {
        }

        public void Place()
        {
            RaiseDomainEvent(new SomethingHappened("placed"));
        }
    }

    [Fact]
    public void A_new_aggregate_has_no_domain_events()
    {
        var order = new Order(Guid.NewGuid());

        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void Raising_a_domain_event_records_it()
    {
        var order = new Order(Guid.NewGuid());

        order.Place();

        var domainEvent = Assert.Single(order.DomainEvents);
        Assert.IsType<SomethingHappened>(domainEvent);
    }

    [Fact]
    public void Clearing_domain_events_empties_the_collection()
    {
        var order = new Order(Guid.NewGuid());
        order.Place();

        order.ClearDomainEvents();

        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void A_domain_event_has_an_identity_and_a_timestamp()
    {
        var domainEvent = new SomethingHappened("x");

        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
        Assert.NotEqual(default, domainEvent.OccurredOnUtc);
    }
}
