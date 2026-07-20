using FactoryOS.Connectors.Log;
using FactoryOS.Connectors.Log.Application;
using FactoryOS.Connectors.Log.Domain;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Tests.ConnectorLog;

public sealed class NotificationDispatchedHandlerTests
{
    private sealed class RecordingEventBus : IEventBus
    {
        public List<IIntegrationEvent> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent integrationEvent, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed record Harness(NotificationDispatchedHandler Handler, RecordingEventBus Bus, IDeliveryJournal Journal);

    private static Harness Build(string transport = "log")
    {
        var bus = new RecordingEventBus();
        var journal = new InMemoryDeliveryJournal();
        var connector = new LogTransportConnector(journal, new LogConnectorOptions { Transport = transport });
        return new Harness(new NotificationDispatchedHandler(bus, connector, new InMemoryProcessedEventLog()), bus, journal);
    }

    private static NotificationDispatched Dispatched(string transport = "log", Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        Channel = "ops",
        Transport = transport,
        Priority = "Critical",
        Subject = "Safety stand-down at site-1",
        Action = "Notify",
        DispatchedAt = DateTimeOffset.UnixEpoch,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task A_dispatch_on_our_transport_is_delivered_and_announced()
    {
        var h = Build(transport: "log");
        var evt = Dispatched(transport: "log");

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Single(h.Journal.ForTenant("acme"));
        var delivered = Assert.Single(h.Bus.Published.OfType<NotificationDelivered>());
        Assert.True(delivered.Delivered);
        Assert.Equal("log", delivered.Transport);
        Assert.Equal(evt.EventId, delivered.SourceEventId);
    }

    [Fact]
    public async Task A_dispatch_on_another_transport_is_ignored()
    {
        var h = Build(transport: "log");
        var evt = Dispatched(transport: "sms"); // not ours

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Journal.ForTenant("acme"));
        Assert.Empty(h.Bus.Published.OfType<NotificationDelivered>());
    }

    [Fact]
    public async Task Redelivery_of_the_same_dispatch_delivers_once()
    {
        var h = Build(transport: "log");
        var evt = Dispatched(transport: "log");

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Journal.ForTenant("acme"));
        Assert.Single(h.Bus.Published.OfType<NotificationDelivered>());
    }
}
