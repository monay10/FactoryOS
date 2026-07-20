using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugins.Dashboard.Api;
using FactoryOS.Plugins.Dashboard.Application;
using FactoryOS.Plugins.Dashboard.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Dashboard;

/// <summary>
/// The Dashboard plugin — the Experience layer's read side. It subscribes to the board-worthy facts other
/// modules emit (OEE and the alert events) and keeps a per-tenant <see cref="IOperationsBoard"/> current, so a
/// wall dashboard or PWA can query one live snapshot without touching any module. It references those modules'
/// events (shared vocabulary) but never the modules themselves. Removing this folder removes the read-model
/// with zero core changes.
/// </summary>
public sealed class DashboardPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "dashboard";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new DashboardOptions());
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.TryAddSingleton<IOperationsBoard>(static sp => new InMemoryOperationsBoard(sp.GetRequiredService<DashboardOptions>()));

        services.AddScoped<IEventHandler<OeeCalculated>, OeeCalculatedHandler>();
        services.AddScoped<IEventHandler<SafetyStandDownTriggered>, SafetyStandDownTriggeredHandler>();
        services.AddScoped<IEventHandler<QualityAlertRaised>, QualityAlertRaisedHandler>();
        services.AddScoped<IEventHandler<LowStockDetected>, LowStockDetectedHandler>();
        services.AddScoped<IEventHandler<EnergySpikeDetected>, EnergySpikeDetectedHandler>();
        services.AddScoped<IEventHandler<DeliveryHealthDegraded>, DeliveryHealthDegradedHandler>();
        services.AddScoped<IEventHandler<WorkOrderClosed>, WorkOrderClosedHandler>();
        services.AddScoped<IEventHandler<QualityLineQuarantined>, QualityLineQuarantinedHandler>();

        services.AddSingleton<IModuleApi>(static sp => new DashboardApi(sp.GetRequiredService<IOperationsBoard>()));
    }
}
