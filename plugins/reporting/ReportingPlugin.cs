using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Reporting.Application;
using FactoryOS.Plugins.Reporting.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Reporting;

/// <summary>
/// The Reporting plugin — an Experience-layer read model that turns the stream of OEE facts into per-machine
/// daily history (average, best, worst). It subscribes to <see cref="OeeCalculated"/> and keeps a per-tenant
/// <see cref="IOeeReport"/> current, so a report or chart can query trends without touching the OEE module. It
/// references the shared event, never any module. Removing this folder removes the report with zero core changes.
/// </summary>
public sealed class ReportingPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "reporting";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new ReportingOptions());
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.TryAddSingleton<IOeeReport>(static sp => new InMemoryOeeReport(sp.GetRequiredService<ReportingOptions>()));

        services.AddScoped<IEventHandler<OeeCalculated>, OeeCalculatedHandler>();
        services.AddScoped<IEventHandler<ScheduledTaskDue>, ScheduledTaskDueHandler>();
    }
}
