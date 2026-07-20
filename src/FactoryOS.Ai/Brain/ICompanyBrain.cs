using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Brain;

/// <summary>
/// The Company Brain: a tenant-aware question-answering facade that grounds answers in the tenant's knowledge
/// base (RAG) and generates them through the LLM Gateway. It composes the retriever, prompt engine and gateway
/// — the three AI-platform pieces — into a single call.
/// </summary>
public interface ICompanyBrain
{
    /// <summary>Answers a question, grounded in the asking tenant's knowledge base.</summary>
    /// <param name="question">The question and the logical models to use.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The grounded answer with citations, or a failure when retrieval, prompting or generation fails.</returns>
    Task<Result<BrainAnswer>> AskAsync(BrainQuestion question, CancellationToken cancellationToken);
}
