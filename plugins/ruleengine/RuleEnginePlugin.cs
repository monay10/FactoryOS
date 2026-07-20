using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.RuleEngine.Application;
using FactoryOS.Plugins.RuleEngine.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.RuleEngine;

/// <summary>
/// The Rule Engine plugin — the Platform-layer turn from observation to action. It subscribes to the Standard
/// Model metric stream (<see cref="MeterReadingReceived"/>), evaluates the tenant's declarative rules and emits
/// <see cref="RuleTriggered"/> for each match. Rules are configuration, not code, so onboarding automation for a
/// factory is a config change. It references the shared events only, never any consuming module. Removing this
/// folder removes rule-based automation with zero core changes.
/// </summary>
public sealed class RuleEnginePlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "ruleengine";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new RuleEngineOptions());
        services.TryAddSingleton<IRuleFiringLog, InMemoryRuleFiringLog>();

        services.AddScoped<IEventHandler<MeterReadingReceived>, MeterReadingReceivedHandler>();
        services.AddScoped<IEventHandler<OeeCalculated>, OeeCalculatedHandler>();
        services.AddScoped<IEventHandler<CarbonEmissionCalculated>, CarbonEmissionCalculatedHandler>();
    }
}
