using FactoryOS.Agents.Insight.Api;
using FactoryOS.Agents.Insight.Application;
using FactoryOS.Agents.Insight.Domain;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Agents.Insight;

/// <summary>
/// The Insight agent — an AI digital worker, wired like any other plugin. It subscribes to alert events and fired
/// rules and, through the LLM Gateway, generates an insight it re-enters onto the bus as <see cref="InsightGenerated"/>. It
/// depends only on the gateway abstraction (resolved from the host) and the shared events — never a module, never
/// an in-process model. Installing or removing this folder adds or removes the digital worker with zero core changes.
/// </summary>
/// <remarks>
/// The host must have registered an <c>ILlmGateway</c> (via <c>AddLlmGateway</c>); the agent consumes it.
/// </remarks>
public sealed class InsightAgentPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>agent.json</c>.</summary>
    public const string PluginKey = "agent.insight";

    /// <summary>The manifest key the read API mounts under (<c>/m/insight/*</c>), matching <c>agent.json</c>.</summary>
    public const string ManifestKey = "insight";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new InsightAgentOptions());
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.TryAddSingleton<InsightEngine>();
        services.TryAddSingleton<IInsightFeed, InMemoryInsightFeed>();

        services.AddScoped<IEventHandler<SafetyStandDownTriggered>, SafetyStandDownTriggeredHandler>();
        services.AddScoped<IEventHandler<QualityAlertRaised>, QualityAlertRaisedHandler>();
        services.AddScoped<IEventHandler<RuleTriggered>, RuleTriggeredHandler>();
        services.AddScoped<IEventHandler<InsightGenerated>, InsightGeneratedHandler>();

        services.AddSingleton<IModuleApi>(static sp => new InsightApi(sp.GetRequiredService<IInsightFeed>()));
    }
}
