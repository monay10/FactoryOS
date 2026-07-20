using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Knowledge;

/// <summary>
/// A tenant-scoped vector store for knowledge chunks. Every operation takes the tenant explicitly; there is no
/// code path that reads or writes across tenants. The in-memory implementation is the default; a
/// pgvector/external store can replace it behind this interface without touching callers.
/// </summary>
public interface IKnowledgeStore
{
    /// <summary>Inserts or replaces chunks (by <see cref="KnowledgeChunk.Id"/>) for a tenant.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="chunks">The embedded chunks to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure describing why the write could not complete.</returns>
    Task<Result> UpsertAsync(string tenant, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken cancellationToken);

    /// <summary>Returns the <paramref name="topK"/> chunks most similar to <paramref name="query"/> for a tenant.</summary>
    /// <param name="tenant">The tenant to search within.</param>
    /// <param name="query">The query embedding vector.</param>
    /// <param name="topK">The maximum number of hits to return, ranked by descending similarity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The ranked hits, or a failure when the query is invalid.</returns>
    Task<Result<IReadOnlyList<ScoredChunk>>> SearchAsync(
        string tenant,
        IReadOnlyList<float> query,
        int topK,
        CancellationToken cancellationToken);
}
