namespace FactoryOS.Contracts.Ai;

/// <summary>
/// A provider-agnostic embedding response. Providers normalize their own dialect (OpenAI, Ollama, …)
/// into this canonical shape — the AI equivalent of the Standard Model for connectors. <see cref="Vectors"/>
/// contains one vector per input, in request order.
/// </summary>
public sealed record EmbeddingResponse
{
    /// <summary>The upstream model that produced the embeddings.</summary>
    public required string Model { get; init; }

    /// <summary>One embedding vector per input text, in the same order as the request inputs.</summary>
    public required IReadOnlyList<IReadOnlyList<float>> Vectors { get; init; }

    /// <summary>Prompt tokens consumed, when reported.</summary>
    public int PromptTokens { get; init; }

    /// <summary>The dimensionality of the returned vectors, or <c>0</c> when no vectors were returned.</summary>
    public int Dimensions => Vectors.Count > 0 ? Vectors[0].Count : 0;
}
