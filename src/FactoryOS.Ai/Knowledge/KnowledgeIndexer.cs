using FactoryOS.Ai.Gateway;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Knowledge;

/// <summary>
/// The default <see cref="IKnowledgeIndexer"/>. Splits a document with <see cref="TextChunker"/>, embeds all
/// chunks in one batch through the <see cref="IEmbeddingGateway"/> and upserts them into the
/// <see cref="IKnowledgeStore"/>. The document's tenant flows onto every chunk so isolation is preserved.
/// </summary>
public sealed class KnowledgeIndexer : IKnowledgeIndexer
{
    private readonly IEmbeddingGateway _embeddings;
    private readonly IKnowledgeStore _store;

    /// <summary>Initializes a new instance of the <see cref="KnowledgeIndexer"/> class.</summary>
    /// <param name="embeddings">The embedding gateway.</param>
    /// <param name="store">The knowledge store.</param>
    public KnowledgeIndexer(IEmbeddingGateway embeddings, IKnowledgeStore store)
    {
        ArgumentNullException.ThrowIfNull(embeddings);
        ArgumentNullException.ThrowIfNull(store);
        _embeddings = embeddings;
        _store = store;
    }

    /// <inheritdoc />
    public async Task<Result<int>> IngestAsync(
        KnowledgeDocument document,
        string embeddingModel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingModel);

        var texts = TextChunker.Chunk(document.Text);
        if (texts.Count == 0)
        {
            return Result.Success(0);
        }

        var embedResult = await _embeddings.EmbedAsync(
            new EmbeddingRequest { Tenant = document.Tenant, Model = embeddingModel, Inputs = texts },
            cancellationToken).ConfigureAwait(false);
        if (embedResult.IsFailure)
        {
            return Result.Failure<int>(embedResult.Error);
        }

        var vectors = embedResult.Value.Vectors;
        if (vectors.Count != texts.Count)
        {
            return Result.Failure<int>(Error.Failure(
                "Ai.Knowledge.EmbeddingCountMismatch",
                $"Expected {texts.Count} vectors for {texts.Count} chunks but got {vectors.Count}."));
        }

        var embedded = new List<EmbeddedChunk>(texts.Count);
        for (var i = 0; i < texts.Count; i++)
        {
            var chunk = new KnowledgeChunk
            {
                Id = $"{document.Source}#{i}",
                Tenant = document.Tenant,
                Source = document.Source,
                Ordinal = i,
                Text = texts[i],
            };
            embedded.Add(new EmbeddedChunk(chunk, vectors[i]));
        }

        var upsert = await _store.UpsertAsync(document.Tenant, embedded, cancellationToken).ConfigureAwait(false);
        return upsert.IsFailure ? Result.Failure<int>(upsert.Error) : Result.Success(embedded.Count);
    }
}
