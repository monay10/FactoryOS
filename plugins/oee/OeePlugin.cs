using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugins.Oee.Api;
using FactoryOS.Plugins.Oee.Application;
using FactoryOS.Plugins.Oee.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Oee;

/// <summary>
/// The OEE plugin. It subscribes to <see cref="ProductionPeriodReported"/> and contributes its snapshot store
/// and handler. Installing or removing this folder adds or removes OEE monitoring with zero core changes — the
/// plugin is self-contained and event-driven.
/// </summary>
public sealed class OeePlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "oee";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new OeeOptions());
        services.TryAddSingleton<IOeeStore, InMemoryOeeStore>();
        services.AddScoped<IEventHandler<ProductionPeriodReported>, ProductionPeriodReportedHandler>();

        services.AddSingleton<IModuleApi>(static sp => new OeeApi(
            sp.GetRequiredService<IOeeStore>(),
            sp.GetRequiredService<OeeOptions>()));
    }
}
