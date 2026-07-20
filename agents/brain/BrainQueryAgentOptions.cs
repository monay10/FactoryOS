namespace FactoryOS.Agents.Brain;

/// <summary>
/// Configuration for the Brain Query agent. The models and retrieval depth are data: a factory chooses which chat
/// and embedding models the Brain answers with, and how much grounding to retrieve, without code changes. The
/// embedding model must match how the knowledge base was indexed (see the Knowledge Ingest agent).
/// </summary>
public sealed record BrainQueryAgentOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Agents:BrainQuery";

    /// <summary>The logical chat model key used to generate answers (for example <c>fast</c> or <c>reasoning</c>).</summary>
    public string ChatModel { get; init; } = "fast";

    /// <summary>The logical embedding model key used to retrieve grounding.</summary>
    public string EmbeddingModel { get; init; } = "embed";

    /// <summary>How many knowledge chunks to ground each answer on.</summary>
    public int TopK { get; init; } = 4;
}
