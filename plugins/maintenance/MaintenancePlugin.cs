using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugins.Maintenance.Api;
using FactoryOS.Plugins.Maintenance.Application;
using FactoryOS.Plugins.Maintenance.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Maintenance;

/// <summary>
/// The Maintenance plugin. It subscribes to <see cref="EnergySpikeDetected"/> and <see cref="RuleTriggered"/> and
/// contributes its work-order store and handlers. Installing or removing this folder adds or removes maintenance
/// work-order automation with zero core changes — the plugin is self-contained and event-driven.
/// </summary>
public sealed class MaintenancePlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "maintenance";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new MaintenanceOptions());
        services.TryAddSingleton<IWorkOrderStore, InMemoryWorkOrderStore>();
        services.AddScoped<IEventHandler<EnergySpikeDetected>, EnergySpikeDetectedHandler>();
        services.AddScoped<IEventHandler<RuleTriggered>, RuleTriggeredHandler>();

        services.AddSingleton<IModuleApi>(static sp => new MaintenanceApi(
            sp.GetRequiredService<IWorkOrderStore>()));
    }
}
