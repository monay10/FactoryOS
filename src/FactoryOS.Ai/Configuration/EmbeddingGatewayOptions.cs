namespace FactoryOS.Ai.Configuration;

/// <summary>
/// Routing configuration for the Embedding Gateway: a table of logical model keys, each mapping to a provider
/// and the concrete upstream model name to call. New embedding models are configuration, not code.
/// </summary>
public sealed class EmbeddingGatewayOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Ai:Embeddings";

    /// <summary>Logical model key → route. Keys are compared case-insensitively.</summary>
    public Dictionary<string, EmbeddingModelRoute> Models { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A single route: which provider serves a logical embedding model, and under what upstream name.</summary>
public sealed class EmbeddingModelRoute
{
    /// <summary>The provider key that serves this model (for example <c>openai</c> or <c>ollama</c>).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The concrete model name to send upstream (for example <c>text-embedding-3-small</c> or <c>nomic-embed-text</c>).</summary>
    public string UpstreamModel { get; set; } = string.Empty;
}
