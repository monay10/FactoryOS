namespace FactoryOS.Ai.Configuration;

/// <summary>
/// Routing configuration for the LLM Gateway: a table of logical model keys, each mapping to a provider and
/// the concrete upstream model name to call. New models are configuration, not code.
/// </summary>
public sealed class LlmGatewayOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Ai:Llm";

    /// <summary>Logical model key → route. Keys are compared case-insensitively.</summary>
    public Dictionary<string, LlmModelRoute> Models { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A single route: which provider serves a logical model, and under what upstream name.</summary>
public sealed class LlmModelRoute
{
    /// <summary>The provider key that serves this model (for example <c>openai</c> or <c>ollama</c>).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The concrete model name to send upstream (for example <c>gpt-4o-mini</c> or <c>llama3</c>).</summary>
    public string UpstreamModel { get; set; } = string.Empty;
}
