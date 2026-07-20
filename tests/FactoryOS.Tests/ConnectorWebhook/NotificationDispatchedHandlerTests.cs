using FactoryOS.Connectors.Webhook;
using FactoryOS.Connectors.Webhook.Application;
using FactoryOS.Connectors.Webhook.Domain;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Tests.ConnectorWebhook;

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

    private sealed class CountingSender : IWebhookSender
    {
        public int Sends { get; private set; }

        public Task<OutboundResult> SendAsync(Uri endpoint, OutboundMessage message, CancellationToken cancellationToken)
        {
            Sends++;
            return Task.FromResult(OutboundResult.Ok("HTTP 200"));
        }
    }

    private sealed record Harness(NotificationDispatchedHandler Handler, RecordingEventBus Bus, CountingSender Sender);

    private static Harness Build()
    {
        var bus = new RecordingEventBus();
        var sender = new CountingSender();
        var options = new WebhookConnectorOptions
        {
            ChannelUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ops"] = "https://hooks.example.com/ops" },
        };
        var connector = new WebhookConnector(sender, options);
        return new Harness(new NotificationDispatchedHandler(bus, connector, new InMemoryProcessedEventLog()), bus, sender);
    }

    private static NotificationDispatched Dispatched(string transport, Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        Channel = "ops",
        Transport = transport,
        Priority = "Critical",
        Subject = "Safety stand-down",
        Action = "Notify",
        DispatchedAt = DateTimeOffset.UnixEpoch,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task A_webhook_dispatch_is_posted_and_announced()
    {
        var h = Build();
        var evt = Dispatched("webhook");

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Equal(1, h.Sender.Sends);
        var delivered = Assert.Single(h.Bus.Published.OfType<NotificationDelivered>());
        Assert.True(delivered.Delivered);
        Assert.Equal("webhook", delivered.Transport);
    }

    [Fact]
    public async Task A_dispatch_on_another_transport_is_ignored()
    {
        var h = Build();
        var evt = Dispatched("log"); // not ours

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Equal(0, h.Sender.Sends);
        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_posts_once()
    {
        var h = Build();
        var evt = Dispatched("webhook");

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Equal(1, h.Sender.Sends);
        Assert.Single(h.Bus.Published.OfType<NotificationDelivered>());
    }
}
