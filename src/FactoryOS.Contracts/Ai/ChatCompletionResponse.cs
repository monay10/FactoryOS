namespace FactoryOS.Contracts.Ai;

/// <summary>
/// A provider-agnostic chat completion response. Providers normalize their own dialect (OpenAI, Ollama, …)
/// into this canonical shape — the AI equivalent of the Standard Model for connectors.
/// </summary>
public sealed record ChatCompletionResponse
{
    /// <summary>The upstream model that produced the completion.</summary>
    public required string Model { get; init; }

    /// <summary>The assistant's reply text.</summary>
    public required string Content { get; init; }

    /// <summary>Why generation stopped (for example <c>stop</c> or <c>length</c>), when reported.</summary>
    public string? FinishReason { get; init; }

    /// <summary>Prompt tokens consumed, when reported.</summary>
    public int PromptTokens { get; init; }

    /// <summary>Completion tokens generated, when reported.</summary>
    public int CompletionTokens { get; init; }

    /// <summary>Total tokens consumed.</summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
