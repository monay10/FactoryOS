namespace FactoryOS.Contracts.Ai;

/// <summary>
/// A question posed to the Company Brain. The brain retrieves grounding from the tenant's knowledge base and
/// answers with a chat model — all resolved from logical model keys, all scoped to <see cref="Tenant"/>.
/// </summary>
public sealed record BrainQuestion
{
    /// <summary>The tenant asking; scopes both retrieval and generation.</summary>
    public required string Tenant { get; init; }

    /// <summary>The natural-language question.</summary>
    public required string Question { get; init; }

    /// <summary>The logical chat model key used to generate the answer.</summary>
    public required string ChatModel { get; init; }

    /// <summary>The logical embedding model key used to retrieve grounding (must match how the base was indexed).</summary>
    public required string EmbeddingModel { get; init; }

    /// <summary>How many knowledge chunks to ground the answer on.</summary>
    public int TopK { get; init; } = 4;
}
