using System.Collections.Concurrent;
using FactoryOS.Ai.Vectors;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Knowledge;

/// <summary>
/// The default in-memory <see cref="IKnowledgeStore"/>: a per-tenant map of chunks, searched by brute-force
/// cosine similarity. Tenants are isolated by construction — each has its own bucket and no query can reach
/// another's. Suitable for development and small corpora; swap for a vector database at scale.
/// </summary>
public sealed class InMemoryKnowledgeStore : IKnowledgeStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, EmbeddedChunk>> _byTenant =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<Result> UpsertAsync(string tenant, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(chunks);

        var bucket = _byTenant.GetOrAdd(tenant, static _ => new ConcurrentDictionary<string, EmbeddedChunk>(StringComparer.Ordinal));
        foreach (var chunk in chunks)
        {
            if (!string.Equals(chunk.Chunk.Tenant, tenant, StringComparison.Ordinal))
            {
                return Task.FromResult(Result.Failure(Error.Failure(
                    "Ai.Knowledge.TenantMismatch",
                    $"Chunk '{chunk.Chunk.Id}' is tagged tenant '{chunk.Chunk.Tenant}' but was upserted under '{tenant}'.")));
            }

            bucket[chunk.Chunk.Id] = chunk;
        }

        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ScoredChunk>>> SearchAsync(
        string tenant,
        IReadOnlyList<float> query,
        int topK,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(query);

        if (topK <= 0)
        {
            return Task.FromResult(Result.Failure<IReadOnlyList<ScoredChunk>>(Error.Failure(
                "Ai.Knowledge.InvalidTopK", "topK must be greater than zero.")));
        }

        if (!_byTenant.TryGetValue(tenant, out var bucket) || bucket.IsEmpty)
        {
            return Task.FromResult(Result.Success<IReadOnlyList<ScoredChunk>>([]));
        }

        var hits = new List<ScoredChunk>();
        foreach (var entry in bucket.Values)
        {
            var similarity = VectorMath.CosineSimilarity(query, entry.Vector);
            if (similarity.IsFailure)
            {
                continue; // dimension mismatch or zero vector — not comparable, skip
            }

            hits.Add(new ScoredChunk { Chunk = entry.Chunk, Score = similarity.Value });
        }

        IReadOnlyList<ScoredChunk> ranked = hits
            .OrderByDescending(static h => h.Score)
            .ThenBy(static h => h.Chunk.Id, StringComparer.Ordinal)
            .Take(topK)
            .ToList();

        return Task.FromResult(Result.Success(ranked));
    }
}
