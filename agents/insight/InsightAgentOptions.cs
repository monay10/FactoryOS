namespace FactoryOS.Agents.Insight;

/// <summary>
/// Configuration for the Insight agent. An agent differs from its peers only by manifest and prompt — the logical
/// model, the system instruction and sampling are all data, so a factory retunes the agent without code changes.
/// </summary>
public sealed record InsightAgentOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Agents:Insight";

    /// <summary>The logical model key the LLM Gateway routes on (for example <c>reasoning</c> or <c>fast</c>).</summary>
    public string Model { get; init; } = "reasoning";

    /// <summary>The system instruction that steers the agent's insights.</summary>
    public string SystemPrompt { get; init; } =
        "You are a factory operations advisor. Given an alert, respond with a concise root-cause hypothesis and " +
        "one recommended action. Be specific and under 80 words.";

    /// <summary>Optional sampling temperature; the provider default is used when null.</summary>
    public decimal? Temperature { get; init; } = 0.2m;

    /// <summary>Optional cap on generated tokens; the provider default is used when null.</summary>
    public int? MaxTokens { get; init; } = 256;
}
