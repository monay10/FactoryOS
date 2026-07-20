using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugins.Activity.Api;
using FactoryOS.Plugins.Activity.Application;
using FactoryOS.Plugins.Activity.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Activity;

/// <summary>
/// The Activity Feed plugin — the factory timeline. It subscribes to the noteworthy events other modules raise
/// (rules firing, work orders, safety stand-downs, quality alerts) and folds each into a per-tenant, newest-first
/// activity feed: a live chronological record that complements the Company Brain's knowledge base. It references
/// those events (shared vocabulary) but never the modules themselves, and consumes several of them alongside
/// other subscribers, so the bus fans out. Removing this folder removes the feed with zero core changes.
/// </summary>
public sealed class ActivityPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "activity";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new ActivityOptions());
        services.TryAddSingleton<IActivityFeed>(static sp => new InMemoryActivityFeed(sp.GetRequiredService<ActivityOptions>()));

        services.AddScoped<IEventHandler<RuleTriggered>, RuleTriggeredHandler>();
        services.AddScoped<IEventHandler<WorkOrderCreated>, WorkOrderCreatedHandler>();
        services.AddScoped<IEventHandler<WorkOrderClosed>, WorkOrderClosedHandler>();
        services.AddScoped<IEventHandler<SafetyStandDownTriggered>, SafetyStandDownTriggeredHandler>();
        services.AddScoped<IEventHandler<QualityAlertRaised>, QualityAlertRaisedHandler>();
        services.AddScoped<IEventHandler<QualityLineQuarantined>, QualityLineQuarantinedHandler>();
        services.AddScoped<IEventHandler<DeliveryHealthDegraded>, DeliveryHealthDegradedHandler>();
        services.AddScoped<IEventHandler<ProductionOrderCompleted>, ProductionOrderCompletedHandler>();
        services.AddScoped<IEventHandler<EnergySpikeDetected>, EnergySpikeDetectedHandler>();
        services.AddScoped<IEventHandler<LowStockDetected>, LowStockDetectedHandler>();
        services.AddScoped<IEventHandler<CertificationGapDetected>, CertificationGapDetectedHandler>();
        services.AddScoped<IEventHandler<InsightGenerated>, InsightGeneratedHandler>();

        services.AddSingleton<IModuleApi>(static sp => new ActivityApi(
            sp.GetRequiredService<IActivityFeed>(),
            sp.GetRequiredService<ActivityOptions>()));
    }
}
