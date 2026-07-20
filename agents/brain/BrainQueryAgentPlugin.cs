using FactoryOS.Agents.Brain.Application;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Agents.Brain;

/// <summary>
/// The Brain Query agent — an AI digital worker that answers questions from the tenant's knowledge base. It
/// subscribes to <see cref="BrainQuestionAsked"/> and, through the Company Brain facade (RAG + LLM Gateway),
/// re-enters a grounded answer as <see cref="BrainAnswered"/>. It depends only on the brain abstraction (resolved
/// from the host) and the shared events — never a module, never an in-process model. Installing or removing this
/// folder adds or removes conversational Q&amp;A over factory knowledge with zero core changes.
/// </summary>
/// <remarks>
/// The host must have registered an <c>ICompanyBrain</c> and its knowledge/embedding/LLM stack (via the AI
/// composition root); the agent consumes it.
/// </remarks>
public sealed class BrainQueryAgentPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>agent.json</c>.</summary>
    public const string PluginKey = "agent.brain";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new BrainQueryAgentOptions());
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();

        services.AddScoped<IEventHandler<BrainQuestionAsked>, BrainQuestionAskedHandler>();
    }
}
