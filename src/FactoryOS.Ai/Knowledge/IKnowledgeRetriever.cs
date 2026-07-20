using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Knowledge;

/// <summary>Retrieves the most relevant knowledge chunks for a query — the "R" in RAG.</summary>
public interface IKnowledgeRetriever
{
    /// <summary>Embeds the query and returns the tenant's most similar chunks.</summary>
    /// <param name="tenant">The tenant to retrieve within.</param>
    /// <param name="query">The natural-language query.</param>
    /// <param name="embeddingModel">The logical embedding model key (must match the one used to index).</param>
    /// <param name="topK">The maximum number of chunks to return.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The ranked chunks, or a failure when embedding or search fails.</returns>
    Task<Result<IReadOnlyList<ScoredChunk>>> RetrieveAsync(
        string tenant,
        string query,
        string embeddingModel,
        int topK,
        CancellationToken cancellationToken);
}
