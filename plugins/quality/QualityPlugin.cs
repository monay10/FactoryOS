using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugins.Quality.Api;
using FactoryOS.Plugins.Quality.Application;
using FactoryOS.Plugins.Quality.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Quality;

/// <summary>
/// The Quality monitoring plugin. It subscribes to <see cref="QualityInspectionRecorded"/> and contributes its
/// rolling defect-rate store, idempotency log and event handler. Installing or removing this folder adds or
/// removes quality monitoring with zero core changes — the plugin is self-contained and event-driven.
/// </summary>
public sealed class QualityPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "quality";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new QualityOptions());
        services.TryAddSingleton<IDefectRateWindowStore>(static sp =>
            new InMemoryDefectRateWindowStore(sp.GetRequiredService<QualityOptions>().WindowSize));
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.TryAddSingleton<IQuarantineStore, InMemoryQuarantineStore>();
        services.AddScoped<IEventHandler<QualityInspectionRecorded>, QualityInspectionRecordedHandler>();

        services.AddSingleton<IModuleApi>(static sp => new QualityApi(
            sp.GetRequiredService<IDefectRateWindowStore>(),
            sp.GetRequiredService<IQuarantineStore>(),
            sp.GetRequiredService<QualityOptions>()));
    }
}
