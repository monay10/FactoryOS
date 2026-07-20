using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Procurement.Application;
using FactoryOS.Plugins.Procurement.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Procurement;

/// <summary>
/// The Procurement plugin. It subscribes to <see cref="LowStockDetected"/> and contributes its requisition store
/// and handler. Installing or removing this folder adds or removes automatic requisitioning with zero core
/// changes — the plugin is self-contained and event-driven. Paired with the Warehouse module it forms a
/// low-stock → requisition chain purely over the bus, with neither module referencing the other.
/// </summary>
public sealed class ProcurementPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "procurement";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new ProcurementOptions());
        services.TryAddSingleton<IPurchaseRequisitionStore, InMemoryPurchaseRequisitionStore>();
        services.AddScoped<IEventHandler<LowStockDetected>, LowStockDetectedHandler>();
    }
}
