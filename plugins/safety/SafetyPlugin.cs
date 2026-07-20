using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Safety.Application;
using FactoryOS.Plugins.Safety.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Safety;

/// <summary>
/// The Safety plugin. It subscribes to <see cref="SafetyIncidentReported"/> and contributes its incident-window
/// store, idempotency log and handler. Installing or removing this folder adds or removes safety monitoring with
/// zero core changes — the plugin is self-contained and event-driven.
/// </summary>
public sealed class SafetyPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "safety";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new SafetyOptions());
        services.TryAddSingleton<IIncidentWindowStore>(static sp =>
            new InMemoryIncidentWindowStore(sp.GetRequiredService<SafetyOptions>().WindowSize));
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.AddScoped<IEventHandler<SafetyIncidentReported>, SafetyIncidentReportedHandler>();
    }
}
