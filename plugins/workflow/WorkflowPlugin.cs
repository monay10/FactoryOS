using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Workflow.Application;
using FactoryOS.Plugins.Workflow.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Workflow;

/// <summary>
/// The Workflow plugin — the configuration-driven bridge from alerts to actions. It subscribes to the alert
/// events other modules raise and, per the tenant's declarative rules, requests actions with
/// <see cref="WorkflowActionRequested"/>. It references those modules' events (shared vocabulary) but never the
/// modules themselves, and the modules never reference Workflow. Installing or removing this folder adds or
/// removes cross-module automation with zero core changes.
/// </summary>
public sealed class WorkflowPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "workflow";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new WorkflowOptions());
        services.TryAddSingleton<IWorkflowRuleSet>(static sp => new WorkflowRuleSet(sp.GetRequiredService<WorkflowOptions>()));
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.TryAddSingleton<WorkflowEngine>();

        services.AddScoped<IEventHandler<SafetyStandDownTriggered>, SafetyStandDownTriggeredHandler>();
        services.AddScoped<IEventHandler<QualityAlertRaised>, QualityAlertRaisedHandler>();
        services.AddScoped<IEventHandler<LowStockDetected>, LowStockDetectedHandler>();
        services.AddScoped<IEventHandler<CertificationGapDetected>, CertificationGapDetectedHandler>();
        services.AddScoped<IEventHandler<WorkOrderCreated>, WorkOrderCreatedHandler>();
        services.AddScoped<IEventHandler<RuleTriggered>, RuleTriggeredHandler>();
        services.AddScoped<IEventHandler<PurchaseRequisitionRaised>, PurchaseRequisitionRaisedHandler>();
    }
}
