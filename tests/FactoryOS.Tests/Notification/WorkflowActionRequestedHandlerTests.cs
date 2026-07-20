using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Notification;
using FactoryOS.Plugins.Notification.Application;
using FactoryOS.Plugins.Notification.Domain;

namespace FactoryOS.Tests.Notification;

public sealed class WorkflowActionRequestedHandlerTests
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

    private sealed record Harness(WorkflowActionRequestedHandler Handler, RecordingEventBus Bus, INotificationOutbox Outbox);

    private static Harness Build(NotificationOptions? options = null)
    {
        var bus = new RecordingEventBus();
        var outbox = new InMemoryNotificationOutbox();
        var opts = options ?? new NotificationOptions
        {
            ChannelTransports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ops"] = "sms" },
        };
        return new Harness(new WorkflowActionRequestedHandler(bus, outbox, opts), bus, outbox);
    }

    private static WorkflowActionRequested Action(Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        TriggerType = "SafetyStandDownTriggered",
        Subject = "Safety stand-down at site-1 (HighSeverity)",
        Action = "Notify",
        Priority = "Critical",
        Channel = "ops",
        OccurredAt = DateTimeOffset.UnixEpoch,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task Dispatches_the_notification_on_the_routed_transport()
    {
        var h = Build();
        var action = Action();

        await h.Handler.HandleAsync(action, Context(action), CancellationToken.None);

        var dispatched = Assert.Single(h.Bus.Published.OfType<NotificationDispatched>());
        Assert.Equal("ops", dispatched.Channel);
        Assert.Equal("sms", dispatched.Transport);
        Assert.Equal("Critical", dispatched.Priority);
        Assert.Equal(action.EventId, dispatched.SourceEventId);

        var recorded = Assert.Single(h.Outbox.ForTenant("acme"));
        Assert.Equal("sms", recorded.Transport);
    }

    [Fact]
    public async Task An_unmapped_channel_uses_the_default_transport()
    {
        var h = Build(new NotificationOptions { DefaultTransport = "log" });
        var action = Action();

        await h.Handler.HandleAsync(action, Context(action), CancellationToken.None);

        var dispatched = Assert.Single(h.Bus.Published.OfType<NotificationDispatched>());
        Assert.Equal("log", dispatched.Transport);
    }

    [Fact]
    public async Task Redelivering_the_same_action_neither_records_nor_reannounces()
    {
        var h = Build();
        var action = Action();

        await h.Handler.HandleAsync(action, Context(action), CancellationToken.None);
        await h.Handler.HandleAsync(action, Context(action), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<NotificationDispatched>());
        Assert.Single(h.Outbox.ForTenant("acme"));
    }
}
