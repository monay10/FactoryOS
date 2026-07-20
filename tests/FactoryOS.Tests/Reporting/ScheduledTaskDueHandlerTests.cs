using System.Text;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Storage;
using FactoryOS.Plugins.Reporting;
using FactoryOS.Plugins.Reporting.Application;
using FactoryOS.Plugins.Reporting.Domain;

namespace FactoryOS.Tests.Reporting;

public sealed class ScheduledTaskDueHandlerTests
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

    private sealed class RecordingObjectStore : IObjectStore
    {
        public List<StoredObject> Puts { get; } = [];

        public Task PutAsync(StoredObject stored, CancellationToken cancellationToken)
        {
            Puts.Add(stored);
            return Task.CompletedTask;
        }

        public Task<StoredObject?> GetAsync(string tenant, string key, CancellationToken cancellationToken) =>
            Task.FromResult(Puts.LastOrDefault(o => o.Tenant == tenant && o.Key == key));

        public Task<bool> ExistsAsync(string tenant, string key, CancellationToken cancellationToken) =>
            Task.FromResult(Puts.Any(o => o.Tenant == tenant && o.Key == key));

        public Task<IReadOnlyList<ObjectRef>> ListAsync(string tenant, string prefix, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ObjectRef>>([]);
    }

    private sealed record Harness(ScheduledTaskDueHandler Handler, RecordingEventBus Bus, RecordingObjectStore Store);

    private static Harness Build()
    {
        var bus = new RecordingEventBus();
        var store = new RecordingObjectStore();
        var report = new InMemoryOeeReport(new ReportingOptions());
        report.Record("acme", "press-1", new DateOnly(2026, 7, 20), 0.9m);
        var handler = new ScheduledTaskDueHandler(bus, store, report, new InMemoryProcessedEventLog(), new ReportingOptions());
        return new Harness(handler, bus, store);
    }

    private static ScheduledTaskDue Due(string action, Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        ScheduleId = "daily-oee",
        Action = action,
        EverySeconds = 86400,
        DueAt = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task The_report_action_stores_a_csv_and_announces_it()
    {
        var h = Build();
        var evt = Due("GenerateReport");

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var put = Assert.Single(h.Store.Puts);
        Assert.Equal("reports/oee/daily-oee.csv", put.Key);
        Assert.Equal("text/csv", put.ContentType);
        Assert.Contains("press-1", Encoding.UTF8.GetString(put.Content.Span), StringComparison.Ordinal);

        var generated = Assert.Single(h.Bus.Published.OfType<ReportGenerated>());
        Assert.Equal("daily-oee", generated.ReportId);
        Assert.Equal("reports/oee/daily-oee.csv", generated.ObjectKey);
        Assert.Equal(1, generated.RowCount);
        Assert.Equal(put.Size, generated.SizeBytes);
        Assert.Equal(evt.DueAt, generated.GeneratedAt);
    }

    [Fact]
    public async Task Another_action_generates_nothing()
    {
        var h = Build();
        var evt = Due("PullErpStock");

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Store.Puts);
        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_announces_only_once()
    {
        var h = Build();
        var evt = Due("GenerateReport");

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<ReportGenerated>());
    }
}
