using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Knowledge;

/// <summary>Ingests documents into a tenant's knowledge base: chunk → embed → store.</summary>
public interface IKnowledgeIndexer
{
    /// <summary>Chunks, embeds and stores a document.</summary>
    /// <param name="document">The document to ingest; its tenant scopes the write.</param>
    /// <param name="embeddingModel">The logical embedding model key to embed chunks with.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of chunks stored, or a failure when embedding or storage fails.</returns>
    Task<Result<int>> IngestAsync(KnowledgeDocument document, string embeddingModel, CancellationToken cancellationToken);
}
