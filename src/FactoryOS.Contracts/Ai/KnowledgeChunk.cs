namespace FactoryOS.Contracts.Ai;

/// <summary>
/// A single retrievable unit of knowledge: one chunk of a source document, scoped to a tenant. Chunks are the
/// granularity at which text is embedded, stored and returned to augment prompts.
/// </summary>
public sealed record KnowledgeChunk
{
    /// <summary>A stable, unique identifier for the chunk (typically <c>{source}#{ordinal}</c>).</summary>
    public required string Id { get; init; }

    /// <summary>The tenant the chunk belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The source document identifier the chunk was derived from.</summary>
    public required string Source { get; init; }

    /// <summary>The zero-based position of the chunk within its source document.</summary>
    public int Ordinal { get; init; }

    /// <summary>The chunk's text.</summary>
    public required string Text { get; init; }
}
