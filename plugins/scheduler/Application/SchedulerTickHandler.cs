using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Scheduler.Domain;

namespace FactoryOS.Plugins.Scheduler.Application;

/// <summary>
/// The Scheduler's consumer of <see cref="SchedulerTick"/>. On each pulse it asks the clock which configured
/// schedules are due for the tenant and emits a <see cref="ScheduledTaskDue"/> for each. The atomic claim keeps
/// a schedule from firing twice per interval, so a redelivered pulse fires nothing new. It references the shared
/// events only, never any consuming module.
/// </summary>
public sealed class SchedulerTickHandler : IEventHandler<SchedulerTick>
{
    private readonly IEventBus _bus;
    private readonly IScheduleClock _clock;
    private readonly SchedulerOptions _options;

    /// <summary>Initializes a new instance of the <see cref="SchedulerTickHandler"/> class.</summary>
    /// <param name="bus">The event bus to announce due schedules on.</param>
    /// <param name="clock">The schedule clock.</param>
    /// <param name="options">The module options carrying the schedules.</param>
    public SchedulerTickHandler(IEventBus bus, IScheduleClock clock, SchedulerOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _clock = clock;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(SchedulerTick integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var schedule in _options.Schedules)
        {
            var interval = TimeSpan.FromSeconds(schedule.EverySeconds);
            if (!_clock.TryClaim(integrationEvent.Tenant, schedule.Id, integrationEvent.Instant, interval))
            {
                continue;
            }

            await _bus.PublishAsync(
                new ScheduledTaskDue
                {
                    Tenant = integrationEvent.Tenant,
                    ScheduleId = schedule.Id,
                    Action = schedule.Action,
                    EverySeconds = schedule.EverySeconds,
                    DueAt = integrationEvent.Instant,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
