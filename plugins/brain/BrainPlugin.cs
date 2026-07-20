using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugins.Brain.Api;
using FactoryOS.Plugins.Brain.Application;
using FactoryOS.Plugins.Brain.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Brain;

/// <summary>
/// The Brain module — the HTTP face of the Company Brain. It keeps a per-tenant, newest-first log of the grounded
/// answers the Brain Query agent re-enters on the bus (<see cref="BrainAnswered"/>) and exposes it as a read API at
/// <c>/m/brain/*</c>, so a UI can show conversational Q&amp;A history without touching the AI layer. It references
/// only the shared event vocabulary, never the agent or the AI stack, and consumes the answer event alongside other
/// subscribers, so the bus fans out. Removing this folder removes the read surface with zero core changes.
/// </summary>
public sealed class BrainPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "brain";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new BrainReadModelOptions());
        services.TryAddSingleton<IBrainAnswerLog>(static sp => new InMemoryBrainAnswerLog(sp.GetRequiredService<BrainReadModelOptions>()));

        services.AddScoped<IEventHandler<BrainAnswered>, BrainAnsweredHandler>();

        services.AddSingleton<IModuleApi>(static sp => new BrainApi(
            sp.GetRequiredService<IBrainAnswerLog>(),
            sp.GetRequiredService<BrainReadModelOptions>()));
    }
}
