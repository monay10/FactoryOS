using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Carbon.Application;
using FactoryOS.Plugins.Carbon.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Carbon;

/// <summary>
/// The Carbon accounting plugin. It subscribes to <see cref="EnergyConsumptionRecorded"/> and contributes its
/// emission ledger, idempotency log and handler. Installing or removing this folder adds or removes carbon
/// accounting with zero core changes — the plugin is self-contained and event-driven. Paired with the Energy
/// module it forms an energy → emission chain purely over the bus, with neither module referencing the other.
/// </summary>
public sealed class CarbonPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "carbon";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new CarbonOptions());
        services.TryAddSingleton<ICarbonLedger, InMemoryCarbonLedger>();
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.AddScoped<IEventHandler<EnergyConsumptionRecorded>, EnergyConsumptionRecordedHandler>();
    }
}
