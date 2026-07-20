namespace FactoryOS.Ai.Configuration;

/// <summary>Connection settings for an OpenAI-compatible backend (OpenAI, Azure OpenAI, vLLM, LM Studio, …).</summary>
public sealed class OpenAiProviderOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Ai:Providers:OpenAi";

    /// <summary>The base URL of the API (the <c>/v1/chat/completions</c> path is appended).</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com";

    /// <summary>The bearer API key; injected from a secret, never hard-coded.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
