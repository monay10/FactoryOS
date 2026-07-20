using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Scheduler.Application;
using FactoryOS.Plugins.Scheduler.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Scheduler;

/// <summary>
/// The Scheduler plugin — the Platform-layer source of "it is time to do X". It subscribes to the host's
/// <see cref="SchedulerTick"/> pulse, evaluates the tenant's configured schedules against a per-tenant clock,
/// and emits <see cref="ScheduledTaskDue"/> for each due schedule. The clock lives outside the modules, so no
/// module invents its own time. It references the shared events, never any consuming module. Removing this
/// folder removes scheduling with zero core changes.
/// </summary>
public sealed class SchedulerPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "scheduler";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new SchedulerOptions());
        services.TryAddSingleton<IScheduleClock, InMemoryScheduleClock>();

        services.AddScoped<IEventHandler<SchedulerTick>, SchedulerTickHandler>();
    }
}
