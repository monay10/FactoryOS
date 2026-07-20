using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.FileStorage;
using FactoryOS.Plugins.Notification;
using FactoryOS.Plugins.Reporting;
using FactoryOS.Plugins.Reporting.Domain;
using FactoryOS.Plugins.Scheduler;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The scheduled-report pipeline extended to its delivery over the real bus, four plugins, zero inter-module
/// references: a host clock pulse makes a report schedule due (Scheduler), the Reporting module renders a CSV and
/// writes it to the object store (File Storage) announcing <see cref="ReportGenerated"/>, and the Notification
/// module routes that report to a transport and announces <see cref="NotificationDispatched"/>. Actual delivery
/// stays a connector's job. `SchedulerTick → … → ReportGenerated → NotificationDispatched`.
/// </summary>
public sealed class ReportToNotificationChainTests
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
    public async Task A_generated_report_is_dispatched_as_a_notification()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton(new SchedulerOptions
        {
            Schedules = [new ScheduleDefinition { Id = "daily-oee", Action = "GenerateReport", EverySeconds = 86400 }],
        });
        services.AddSingleton(new NotificationOptions
        {
            ChannelTransports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["reports"] = "email" },
        });

        new SchedulerPlugin().ConfigureServices(services);
        new ReportingPlugin().ConfigureServices(services);
        new FileStoragePlugin().ConfigureServices(services);
        new NotificationPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<NotificationDispatched>, CapturingHandler<NotificationDispatched>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        var report = provider.GetRequiredService<IOeeReport>();
        report.Record("acme", "press-1", new DateOnly(2026, 7, 20), 0.88m);

        await bus.PublishAsync(new SchedulerTick
        {
            Tenant = "acme",
            Instant = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
        });

        var dispatched = Assert.Single(sink.Events.OfType<NotificationDispatched>());
        Assert.Equal("acme", dispatched.Tenant);
        Assert.Equal("reports", dispatched.Channel);
        Assert.Equal("email", dispatched.Transport);
        Assert.Contains("daily-oee", dispatched.Subject, StringComparison.Ordinal);
        Assert.Contains("reports/oee/daily-oee.csv", dispatched.Subject, StringComparison.Ordinal);
    }
}
