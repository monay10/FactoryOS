using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Scheduler;
using FactoryOS.Plugins.Scheduler.Application;
using FactoryOS.Plugins.Scheduler.Domain;

namespace FactoryOS.Tests.Scheduler;

public sealed class SchedulerTickHandlerTests
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

    private sealed record Harness(SchedulerTickHandler Handler, RecordingEventBus Bus);

    private static Harness Build(params ScheduleDefinition[] schedules)
    {
        var bus = new RecordingEventBus();
        var handler = new SchedulerTickHandler(bus, new InMemoryScheduleClock(), new SchedulerOptions { Schedules = schedules });
        return new Harness(handler, bus);
    }

    private static SchedulerTick Tick(DateTimeOffset instant) => new() { Tenant = "acme", Instant = instant };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static readonly DateTimeOffset T0 = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Due_schedules_are_announced_with_their_action()
    {
        var h = Build(
            new ScheduleDefinition { Id = "hourly", Action = "PullErpStock", EverySeconds = 3600 },
            new ScheduleDefinition { Id = "daily", Action = "GenerateReport", EverySeconds = 86400 });

        var tick = Tick(T0);
        await h.Handler.HandleAsync(tick, Context(tick), CancellationToken.None);

        var due = h.Bus.Published.OfType<ScheduledTaskDue>().ToList();
        Assert.Equal(2, due.Count); // both fire on their first pulse
        Assert.Contains(due, d => d is { ScheduleId: "hourly", Action: "PullErpStock" });
        Assert.Contains(due, d => d is { ScheduleId: "daily", DueAt: var at } && at == T0);
    }

    [Fact]
    public async Task A_schedule_does_not_fire_again_within_its_interval()
    {
        var h = Build(new ScheduleDefinition { Id = "hourly", Action = "PullErpStock", EverySeconds = 3600 });

        var first = Tick(T0);
        await h.Handler.HandleAsync(first, Context(first), CancellationToken.None);
        var soon = Tick(T0.AddMinutes(30));
        await h.Handler.HandleAsync(soon, Context(soon), CancellationToken.None);

        Assert.Single(h.Bus.Published.OfType<ScheduledTaskDue>());
    }

    [Fact]
    public async Task A_schedule_fires_again_after_its_interval()
    {
        var h = Build(new ScheduleDefinition { Id = "hourly", Action = "PullErpStock", EverySeconds = 3600 });

        var first = Tick(T0);
        await h.Handler.HandleAsync(first, Context(first), CancellationToken.None);
        var later = Tick(T0.AddHours(1));
        await h.Handler.HandleAsync(later, Context(later), CancellationToken.None);

        Assert.Equal(2, h.Bus.Published.OfType<ScheduledTaskDue>().Count());
    }
}
