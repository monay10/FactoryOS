using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Production.Application;
using FactoryOS.Plugins.Production.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Production;

/// <summary>
/// The Production tracking plugin. It subscribes to <see cref="ProductionOrderReleased"/> and
/// <see cref="ProductionCountReported"/> and contributes its order-progress store, idempotency log and handlers.
/// Installing or removing this folder adds or removes production tracking with zero core changes — the plugin is
/// self-contained and event-driven.
/// </summary>
public sealed class ProductionPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "production";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new ProductionOptions());
        services.TryAddSingleton<IProductionOrderStore>(static sp =>
            new InMemoryProductionOrderStore(sp.GetRequiredService<ProductionOptions>().AllowOverProduction));
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.AddScoped<IEventHandler<ProductionOrderReleased>, ProductionOrderReleasedHandler>();
        services.AddScoped<IEventHandler<ProductionCountReported>, ProductionCountReportedHandler>();
    }
}
