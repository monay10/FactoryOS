namespace FactoryOS.Contracts.Ai;

/// <summary>A retrieval hit: a <see cref="KnowledgeChunk"/> paired with its similarity to the query.</summary>
public sealed record ScoredChunk
{
    /// <summary>The retrieved chunk.</summary>
    public required KnowledgeChunk Chunk { get; init; }

    /// <summary>The cosine similarity to the query vector, in <c>[-1, 1]</c>; higher is more relevant.</summary>
    public required double Score { get; init; }
}
