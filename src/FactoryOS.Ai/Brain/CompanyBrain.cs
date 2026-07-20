using FactoryOS.Ai.Gateway;
using FactoryOS.Ai.Knowledge;
using FactoryOS.Ai.Prompts;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Brain;

/// <summary>
/// The default <see cref="ICompanyBrain"/>. Pipeline: retrieve grounding chunks → render them into a context
/// block → compose the grounded-answer prompt → generate through the LLM Gateway → return the answer with the
/// chunks it was grounded on as citations. Every step is tenant-scoped and any failure short-circuits.
/// </summary>
public sealed class CompanyBrain : ICompanyBrain
{
    private readonly IKnowledgeRetriever _retriever;
    private readonly IPromptComposer _composer;
    private readonly ILlmGateway _llm;

    /// <summary>Initializes a new instance of the <see cref="CompanyBrain"/> class.</summary>
    /// <param name="retriever">The knowledge retriever.</param>
    /// <param name="composer">The prompt composer (the grounded-answer template must be registered).</param>
    /// <param name="llm">The LLM gateway.</param>
    public CompanyBrain(IKnowledgeRetriever retriever, IPromptComposer composer, ILlmGateway llm)
    {
        ArgumentNullException.ThrowIfNull(retriever);
        ArgumentNullException.ThrowIfNull(composer);
        ArgumentNullException.ThrowIfNull(llm);
        _retriever = retriever;
        _composer = composer;
        _llm = llm;
    }

    /// <inheritdoc />
    public async Task<Result<BrainAnswer>> AskAsync(BrainQuestion question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);

        var retrieval = await _retriever.RetrieveAsync(
            question.Tenant, question.Question, question.EmbeddingModel, question.TopK, cancellationToken)
            .ConfigureAwait(false);
        if (retrieval.IsFailure)
        {
            return Result.Failure<BrainAnswer>(retrieval.Error);
        }

        var chunks = retrieval.Value;
        var context = RagContext.Build(chunks);

        var messages = _composer.Compose(BrainPrompts.AnswerKey, new Dictionary<string, string>
        {
            ["context"] = context,
            ["question"] = question.Question,
        });
        if (messages.IsFailure)
        {
            return Result.Failure<BrainAnswer>(messages.Error);
        }

        var completion = await _llm.CompleteAsync(
            new ChatCompletionRequest
            {
                Tenant = question.Tenant,
                Model = question.ChatModel,
                Messages = messages.Value,
            },
            cancellationToken).ConfigureAwait(false);
        if (completion.IsFailure)
        {
            return Result.Failure<BrainAnswer>(completion.Error);
        }

        var citations = new List<BrainCitation>(chunks.Count);
        foreach (var chunk in chunks)
        {
            citations.Add(new BrainCitation
            {
                Source = chunk.Chunk.Source,
                ChunkId = chunk.Chunk.Id,
                Score = chunk.Score,
            });
        }

        return Result.Success(new BrainAnswer
        {
            Answer = completion.Value.Content,
            Citations = citations,
            Model = completion.Value.Model,
        });
    }
}
