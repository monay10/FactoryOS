using FactoryOS.Ai.Gateway;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Knowledge;

/// <summary>
/// The default <see cref="IKnowledgeRetriever"/>. Embeds the query through the <see cref="IEmbeddingGateway"/>
/// and asks the <see cref="IKnowledgeStore"/> for the nearest chunks in the tenant's namespace.
/// </summary>
public sealed class KnowledgeRetriever : IKnowledgeRetriever
{
    private readonly IEmbeddingGateway _embeddings;
    private readonly IKnowledgeStore _store;

    /// <summary>Initializes a new instance of the <see cref="KnowledgeRetriever"/> class.</summary>
    /// <param name="embeddings">The embedding gateway.</param>
    /// <param name="store">The knowledge store.</param>
    public KnowledgeRetriever(IEmbeddingGateway embeddings, IKnowledgeStore store)
    {
        ArgumentNullException.ThrowIfNull(embeddings);
        ArgumentNullException.ThrowIfNull(store);
        _embeddings = embeddings;
        _store = store;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ScoredChunk>>> RetrieveAsync(
        string tenant,
        string query,
        string embeddingModel,
        int topK,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingModel);

        var embedResult = await _embeddings.EmbedAsync(
            new EmbeddingRequest { Tenant = tenant, Model = embeddingModel, Inputs = [query] },
            cancellationToken).ConfigureAwait(false);
        if (embedResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ScoredChunk>>(embedResult.Error);
        }

        if (embedResult.Value.Vectors.Count == 0)
        {
            return Result.Failure<IReadOnlyList<ScoredChunk>>(Error.Failure(
                "Ai.Knowledge.EmptyQueryEmbedding", "The query produced no embedding vector."));
        }

        return await _store.SearchAsync(tenant, embedResult.Value.Vectors[0], topK, cancellationToken).ConfigureAwait(false);
    }
}
