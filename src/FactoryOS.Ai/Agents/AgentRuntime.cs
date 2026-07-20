using FactoryOS.Ai.Gateway;
using FactoryOS.Ai.Knowledge;
using FactoryOS.Ai.Prompts;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Agents;

/// <summary>
/// The default <see cref="IAgentRuntime"/>. It resolves the agent manifest, renders its system prompt (binding
/// any variables), optionally grounds the task in the tenant's knowledge base (RAG), then generates through the
/// LLM Gateway. No agent-specific code lives here — every agent is data plus this one runtime.
/// </summary>
public sealed class AgentRuntime : IAgentRuntime
{
    private static readonly IReadOnlyDictionary<string, string> NoVariables =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly IAgentCatalog _catalog;
    private readonly IPromptRenderer _renderer;
    private readonly IKnowledgeRetriever _retriever;
    private readonly ILlmGateway _llm;

    /// <summary>Initializes a new instance of the <see cref="AgentRuntime"/> class.</summary>
    /// <param name="catalog">The agent catalog.</param>
    /// <param name="renderer">The strict prompt renderer for system-prompt variables.</param>
    /// <param name="retriever">The knowledge retriever used when an agent declares grounding.</param>
    /// <param name="llm">The LLM gateway.</param>
    public AgentRuntime(
        IAgentCatalog catalog,
        IPromptRenderer renderer,
        IKnowledgeRetriever retriever,
        ILlmGateway llm)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(retriever);
        ArgumentNullException.ThrowIfNull(llm);
        _catalog = catalog;
        _renderer = renderer;
        _retriever = retriever;
        _llm = llm;
    }

    /// <inheritdoc />
    public async Task<Result<AgentResponse>> RunAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_catalog.TryGet(request.AgentKey, out var definition))
        {
            return Result.Failure<AgentResponse>(Error.NotFound(
                "Ai.Agent.UnknownAgent", $"No agent is registered with key '{request.AgentKey}'."));
        }

        var system = _renderer.Render(definition.SystemPrompt, request.Variables ?? NoVariables);
        if (system.IsFailure)
        {
            return Result.Failure<AgentResponse>(system.Error);
        }

        IReadOnlyList<ScoredChunk> grounding = [];
        var userContent = request.Input;

        if (definition.Grounding is { } gr)
        {
            var retrieval = await _retriever.RetrieveAsync(
                request.Tenant, request.Input, gr.EmbeddingModel, gr.TopK, cancellationToken)
                .ConfigureAwait(false);
            if (retrieval.IsFailure)
            {
                return Result.Failure<AgentResponse>(retrieval.Error);
            }

            grounding = retrieval.Value;
            userContent = $"Context:\n{RagContext.Build(grounding)}\n\nTask: {request.Input}";
        }

        var completion = await _llm.CompleteAsync(
            new ChatCompletionRequest
            {
                Tenant = request.Tenant,
                Model = definition.ChatModel,
                Messages =
                [
                    new ChatMessage(ChatRole.System, system.Value),
                    new ChatMessage(ChatRole.User, userContent),
                ],
            },
            cancellationToken).ConfigureAwait(false);
        if (completion.IsFailure)
        {
            return Result.Failure<AgentResponse>(completion.Error);
        }

        return Result.Success(new AgentResponse
        {
            AgentKey = definition.Key,
            Output = completion.Value.Content,
            Model = completion.Value.Model,
            Grounding = grounding,
        });
    }
}
