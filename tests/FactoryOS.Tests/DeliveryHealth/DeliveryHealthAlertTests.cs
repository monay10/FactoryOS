using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.DeliveryHealth;
using FactoryOS.Plugins.DeliveryHealth.Application;
using FactoryOS.Plugins.DeliveryHealth.Domain;

namespace FactoryOS.Tests.DeliveryHealth;

public sealed class DeliveryHealthAlertTests
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

    private static readonly DateTimeOffset At = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);

    private static NotificationDelivered Delivered(bool delivered, string? detail = null) => new()
    {
        EventId = Guid.NewGuid(),
        Tenant = "acme",
        Transport = "webhook",
        Channel = "ops",
        Subject = "s",
        Delivered = delivered,
        Detail = detail,
        DeliveredAt = At,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static (NotificationDeliveredHandler Handler, RecordingEventBus Bus) Build(int threshold)
    {
        var bus = new RecordingEventBus();
        var store = new InMemoryDeliveryHealthStore(new DeliveryHealthOptions());
        var handler = new NotificationDeliveredHandler(bus, store, new DeliveryHealthOptions { FailureThreshold = threshold });
        return (handler, bus);
    }

    private static async Task Feed(NotificationDeliveredHandler handler, NotificationDelivered evt) =>
        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

    [Fact]
    public async Task Degradation_is_raised_once_when_the_streak_reaches_the_threshold()
    {
        var (handler, bus) = Build(threshold: 3);

        await Feed(handler, Delivered(false, "500"));
        await Feed(handler, Delivered(false, "502"));
        Assert.Empty(bus.Published); // streak 1, 2 — below threshold

        await Feed(handler, Delivered(false, "503")); // streak 3 — crosses
        var alert = Assert.Single(bus.Published.OfType<DeliveryHealthDegraded>());
        Assert.Equal("webhook", alert.Transport);
        Assert.Equal(3, alert.ConsecutiveFailures);
        Assert.Equal(3, alert.Failed);
        Assert.Equal("503", alert.LastDetail);

        await Feed(handler, Delivered(false, "504")); // streak 4 — no re-alert
        Assert.Single(bus.Published.OfType<DeliveryHealthDegraded>());
    }

    [Fact]
    public async Task A_success_resets_the_streak_so_it_can_alert_again()
    {
        var (handler, bus) = Build(threshold: 2);

        await Feed(handler, Delivered(false));
        await Feed(handler, Delivered(false)); // crosses → alert 1
        await Feed(handler, Delivered(true));  // reset
        await Feed(handler, Delivered(false));
        await Feed(handler, Delivered(false)); // crosses again → alert 2

        Assert.Equal(2, bus.Published.OfType<DeliveryHealthDegraded>().Count());
    }

    [Fact]
    public async Task A_healthy_transport_never_alerts()
    {
        var (handler, bus) = Build(threshold: 1);

        await Feed(handler, Delivered(true));
        await Feed(handler, Delivered(true));

        Assert.Empty(bus.Published);
    }
}
