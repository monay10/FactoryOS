using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Scheduler;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Scheduler proven over the real bus: a host clock pulse (<see cref="SchedulerTick"/>) drives the module to
/// emit <see cref="ScheduledTaskDue"/> for each configured schedule, and a second pulse within the interval
/// emits nothing — the whole due-decision travels the bus with the clock owned by the host, not the module.
/// </summary>
public sealed class SchedulerTickTests
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
    public async Task A_pulse_makes_due_schedules_fire_exactly_once_per_interval()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton(new SchedulerOptions
        {
            Schedules = [new ScheduleDefinition { Id = "erp-stock-pull", Action = "PullErpStock", EverySeconds = 3600 }],
        });
        new SchedulerPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<ScheduledTaskDue>, CapturingHandler<ScheduledTaskDue>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        var t0 = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        await bus.PublishAsync(new SchedulerTick { Tenant = "acme", Instant = t0 });
        await bus.PublishAsync(new SchedulerTick { Tenant = "acme", Instant = t0.AddMinutes(30) }); // within interval

        var due = Assert.Single(sink.Events.OfType<ScheduledTaskDue>());
        Assert.Equal("erp-stock-pull", due.ScheduleId);
        Assert.Equal("PullErpStock", due.Action);
        Assert.Equal(t0, due.DueAt);
    }
}
