using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugins.Energy.Api;
using FactoryOS.Plugins.Energy.Application;
using FactoryOS.Plugins.Energy.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Energy;

/// <summary>
/// The Energy monitoring plugin. It subscribes to <see cref="MeterReadingReceived"/> and contributes its
/// baseline store, idempotency log and event handler. Installing or removing this folder adds or removes
/// energy monitoring with zero core changes — the plugin is self-contained and event-driven.
/// </summary>
public sealed class EnergyPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "energy";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new EnergyOptions());
        services.TryAddSingleton<IEnergyBaselineStore>(static sp =>
            new InMemoryEnergyBaselineStore(sp.GetRequiredService<EnergyOptions>().BaselineWindow));
        services.TryAddSingleton<IEnergyReadModel>(static sp =>
            new InMemoryEnergyReadModel(sp.GetRequiredService<EnergyOptions>()));
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.AddScoped<IEventHandler<MeterReadingReceived>, MeterReadingReceivedHandler>();

        services.AddSingleton<IModuleApi>(static sp => new EnergyApi(sp.GetRequiredService<IEnergyReadModel>()));
    }
}
