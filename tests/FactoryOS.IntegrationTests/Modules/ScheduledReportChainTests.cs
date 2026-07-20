using System.Collections.Concurrent;
using System.Text;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Storage;
using FactoryOS.Plugins.FileStorage;
using FactoryOS.Plugins.Reporting;
using FactoryOS.Plugins.Reporting.Domain;
using FactoryOS.Plugins.Scheduler;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The scheduled-report pipeline over the real bus, three plugins, zero inter-module references: a host clock
/// pulse makes a report schedule due (Scheduler → <see cref="ScheduledTaskDue"/>), which the Reporting module
/// renders from its OEE read-model into a CSV and writes to the object store (File Storage), announcing
/// <see cref="ReportGenerated"/>. Scheduler, Reporting and File Storage compose only through shared contracts.
/// </summary>
public sealed class ScheduledReportChainTests
{
    private sealed class CaptureSink
    {
        public ConcurrentBag<IIntegrationEvent> Events { get; } = [];
    }

    private sealed class CapturingHandler<TEvent> : IEventHandler<TEvent>
        where TEvent : IIntegrationEvent
    {
        private readonly CaptureSink _sink;

        public CapturingHandler(CaptureSink sink) => _sink = sink;

        public Task HandleAsync(TEvent integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _sink.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_due_report_schedule_renders_and_stores_a_csv_artifact()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton(new SchedulerOptions
        {
            Schedules = [new ScheduleDefinition { Id = "daily-oee", Action = "GenerateReport", EverySeconds = 86400 }],
        });

        new SchedulerPlugin().ConfigureServices(services);
        new ReportingPlugin().ConfigureServices(services);
        new FileStoragePlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<ReportGenerated>, CapturingHandler<ReportGenerated>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var store = provider.GetRequiredService<IObjectStore>();

        // Seed the OEE read-model the report renders from.
        var report = provider.GetRequiredService<IOeeReport>();
        report.Record("acme", "press-1", new DateOnly(2026, 7, 20), 0.88m);
        report.Record("acme", "oven-2", new DateOnly(2026, 7, 20), 0.72m);

        await bus.PublishAsync(new SchedulerTick
        {
            Tenant = "acme",
            Instant = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
        });

        var generated = Assert.Single(sink.Events.OfType<ReportGenerated>());
        Assert.Equal("daily-oee", generated.ReportId);
        Assert.Equal("reports/oee/daily-oee.csv", generated.ObjectKey);
        Assert.Equal(2, generated.RowCount);

        var stored = await store.GetAsync("acme", "reports/oee/daily-oee.csv", CancellationToken.None);
        Assert.NotNull(stored);
        var csv = Encoding.UTF8.GetString(stored.Content.Span);
        Assert.Contains("press-1", csv, StringComparison.Ordinal);
        Assert.Contains("oven-2", csv, StringComparison.Ordinal);

        // The artifact is the asking tenant's alone.
        Assert.Null(await store.GetAsync("globex", "reports/oee/daily-oee.csv", CancellationToken.None));
    }
}
