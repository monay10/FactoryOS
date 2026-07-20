using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Notification;
using FactoryOS.Plugins.Notification.Application;
using FactoryOS.Plugins.Notification.Domain;

namespace FactoryOS.Tests.Notification;

public sealed class BrainAnsweredHandlerTests
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

    private sealed record Harness(BrainAnsweredHandler Handler, RecordingEventBus Bus, INotificationOutbox Outbox);

    private static Harness Build(NotificationOptions? options = null)
    {
        var bus = new RecordingEventBus();
        var outbox = new InMemoryNotificationOutbox();
        var handler = new BrainAnsweredHandler(bus, outbox, options ?? new NotificationOptions());
        return new Harness(handler, bus, outbox);
    }

    private static BrainAnswered Answered(Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        Question = "how often to lubricate the pump?",
        Answer = "Lubricate the pump bearings monthly.",
        Model = "fast-upstream",
        Citations = [new BrainCitation { Source = "pump-manual", ChunkId = "pump-manual#0", Score = 0.9 }],
        AnsweredAt = new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero),
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task An_answer_is_routed_recorded_and_announced()
    {
        var h = Build(new NotificationOptions
        {
            ChannelTransports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["assistant"] = "chat" },
        });
        var evt = Answered();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var dispatched = Assert.Single(h.Bus.Published.OfType<NotificationDispatched>());
        Assert.Equal("acme", dispatched.Tenant);
        Assert.Equal("assistant", dispatched.Channel);
        Assert.Equal("chat", dispatched.Transport);
        Assert.Equal(evt.AnsweredAt, dispatched.DispatchedAt);
        Assert.Equal(evt.EventId, dispatched.SourceEventId);
        Assert.Contains("Lubricate the pump bearings monthly.", dispatched.Subject, StringComparison.Ordinal);
        Assert.Contains("1 source(s)", dispatched.Subject, StringComparison.Ordinal);

        var recorded = Assert.Single(h.Outbox.ForTenant("acme"));
        Assert.Equal("chat", recorded.Transport);
    }

    [Fact]
    public async Task An_unmapped_assistant_channel_falls_back_to_the_default_transport()
    {
        var h = Build(new NotificationOptions { DefaultTransport = "log" });
        var evt = Answered();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Equal("log", Assert.Single(h.Bus.Published.OfType<NotificationDispatched>()).Transport);
    }

    [Fact]
    public async Task Redelivery_dispatches_only_once()
    {
        var h = Build();
        var evt = Answered();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<NotificationDispatched>());
        Assert.Single(h.Outbox.ForTenant("acme"));
    }
}
