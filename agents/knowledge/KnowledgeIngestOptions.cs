namespace FactoryOS.Agents.Knowledge;

/// <summary>
/// Configuration for the Knowledge Ingest agent. The logical embedding model is data, so a factory retargets the
/// embedding provider (OpenAI-compatible, Ollama, …) without code changes.
/// </summary>
public sealed record KnowledgeIngestOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Agents:Knowledge";

    /// <summary>The logical embedding model key the Embedding Gateway routes on (for example <c>embed</c>).</summary>
    public string EmbeddingModel { get; init; } = "embed";
}
