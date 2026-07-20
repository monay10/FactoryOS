namespace FactoryOS.Ai.Configuration;

/// <summary>Connection settings for a local Ollama backend.</summary>
public sealed class OllamaProviderOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Ai:Providers:Ollama";

    /// <summary>The base URL of the Ollama server (the <c>/api/chat</c> path is appended).</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";
}
