using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugins.DeliveryHealth.Api;
using FactoryOS.Plugins.DeliveryHealth.Application;
using FactoryOS.Plugins.DeliveryHealth.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.DeliveryHealth;

/// <summary>
/// The Delivery Health plugin — the observability read model over notification delivery. It subscribes to
/// <see cref="NotificationDelivered"/> and folds each outcome into per-tenant, per-transport tallies plus a bounded
/// recent-failure list, so a UI or an AI agent can judge transport health without referencing the connectors or the
/// Notification module. When a transport's consecutive-failure streak reaches the configured threshold it raises
/// <see cref="DeliveryHealthDegraded"/> on the bus. It references only the shared events. Removing this folder
/// removes delivery observability with zero core changes.
/// </summary>
public sealed class DeliveryHealthPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "deliveryhealth";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new DeliveryHealthOptions());
        services.TryAddSingleton<IDeliveryHealthStore>(static sp =>
            new InMemoryDeliveryHealthStore(sp.GetRequiredService<DeliveryHealthOptions>()));

        services.AddScoped<IEventHandler<NotificationDelivered>, NotificationDeliveredHandler>();

        services.AddSingleton<IModuleApi>(static sp => new DeliveryHealthApi(
            sp.GetRequiredService<IDeliveryHealthStore>(),
            sp.GetRequiredService<DeliveryHealthOptions>()));
    }
}
