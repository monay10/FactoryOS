using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Notification;
using FactoryOS.Plugins.Notification.Application;
using FactoryOS.Plugins.Notification.Domain;

namespace FactoryOS.Tests.Notification;

public sealed class ReportGeneratedHandlerTests
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

    private sealed record Harness(ReportGeneratedHandler Handler, RecordingEventBus Bus, INotificationOutbox Outbox);

    private static Harness Build(NotificationOptions? options = null)
    {
        var bus = new RecordingEventBus();
        var outbox = new InMemoryNotificationOutbox();
        var handler = new ReportGeneratedHandler(bus, outbox, options ?? new NotificationOptions());
        return new Harness(handler, bus, outbox);
    }

    private static ReportGenerated Report(Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        ReportId = "sched-oee-daily",
        ObjectKey = "reports/oee/sched-oee-daily.csv",
        ContentType = "text/csv",
        SizeBytes = 512,
        RowCount = 12,
        GeneratedAt = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task A_report_is_routed_recorded_and_announced()
    {
        var h = Build(new NotificationOptions
        {
            ChannelTransports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["reports"] = "email" },
        });
        var evt = Report();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var dispatched = Assert.Single(h.Bus.Published.OfType<NotificationDispatched>());
        Assert.Equal("acme", dispatched.Tenant);
        Assert.Equal("reports", dispatched.Channel);
        Assert.Equal("email", dispatched.Transport);
        Assert.Equal("Normal", dispatched.Priority);
        Assert.Equal("Notify", dispatched.Action);
        Assert.Equal(evt.GeneratedAt, dispatched.DispatchedAt);
        Assert.Equal(evt.EventId, dispatched.SourceEventId);
        Assert.Contains("sched-oee-daily", dispatched.Subject, StringComparison.Ordinal);
        Assert.Contains("12 rows", dispatched.Subject, StringComparison.Ordinal);
        Assert.Contains("reports/oee/sched-oee-daily.csv", dispatched.Subject, StringComparison.Ordinal);

        var recorded = Assert.Single(h.Outbox.ForTenant("acme"));
        Assert.Equal("email", recorded.Transport);
        Assert.Equal(evt.GeneratedAt, recorded.DispatchedAt);
    }

    [Fact]
    public async Task An_unmapped_report_channel_falls_back_to_the_default_transport()
    {
        var h = Build(new NotificationOptions { DefaultTransport = "log" });
        var evt = Report();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var dispatched = Assert.Single(h.Bus.Published.OfType<NotificationDispatched>());
        Assert.Equal("log", dispatched.Transport);
    }

    [Fact]
    public async Task Redelivery_dispatches_only_once()
    {
        var h = Build();
        var evt = Report();

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<NotificationDispatched>());
        Assert.Single(h.Outbox.ForTenant("acme"));
    }
}
