using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.DigitalTwin.Application;
using FactoryOS.Plugins.DigitalTwin.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.DigitalTwin;

/// <summary>
/// The Digital Twin plugin — an Experience-layer read model that mirrors each physical asset's live state. It
/// subscribes to telemetry (<see cref="MeterReadingReceived"/>) and health (<see cref="OeeCalculated"/>) and
/// keeps a per-tenant <see cref="IAssetTwinRegistry"/> current, so an operator or 3D view can query one asset's
/// current gauges, health and status without touching any module. It references the shared events, never any
/// module. Removing this folder removes the twin with zero core changes.
/// </summary>
public sealed class DigitalTwinPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "digitaltwin";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new DigitalTwinOptions());
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.TryAddSingleton<IAssetTwinRegistry>(static sp =>
            new InMemoryAssetTwinRegistry(sp.GetRequiredService<DigitalTwinOptions>()));

        services.AddScoped<IEventHandler<MeterReadingReceived>, MeterReadingReceivedHandler>();
        services.AddScoped<IEventHandler<OeeCalculated>, OeeCalculatedHandler>();
    }
}
