using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugins.Warehouse.Api;
using FactoryOS.Plugins.Warehouse.Application;
using FactoryOS.Plugins.Warehouse.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Warehouse;

/// <summary>
/// The Warehouse plugin. It subscribes to <see cref="StockMovementRecorded"/> and
/// <see cref="ItemReorderPointDefined"/> and contributes its stock ledger, idempotency log and handlers.
/// Installing or removing this folder adds or removes inventory tracking with zero core changes — the plugin is
/// self-contained and event-driven.
/// </summary>
public sealed class WarehousePlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "warehouse";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new WarehouseOptions());
        services.TryAddSingleton<IStockLedger, InMemoryStockLedger>();
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.AddScoped<IEventHandler<StockMovementRecorded>, StockMovementRecordedHandler>();
        services.AddScoped<IEventHandler<ItemReorderPointDefined>, ItemReorderPointDefinedHandler>();

        services.AddSingleton<IModuleApi>(static sp => new WarehouseApi(
            sp.GetRequiredService<IStockLedger>(),
            sp.GetRequiredService<WarehouseOptions>()));
    }
}
