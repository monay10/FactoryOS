using System.Globalization;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Agents.Insight;

/// <summary>
/// Builds the chat request the agent sends to the LLM Gateway from a signal and the agent's options. Pure: the
/// same signal and options always produce the same request, which makes the agent's prompting testable without
/// a model.
/// </summary>
public static class InsightPromptBuilder
{
    /// <summary>Builds a tenant-scoped chat completion request for a signal.</summary>
    /// <param name="signal">The normalized trigger.</param>
    /// <param name="options">The agent options carrying the model, system prompt and sampling.</param>
    /// <returns>A request whose <c>Model</c> is a logical FactoryOS model key.</returns>
    public static ChatCompletionRequest Build(InsightSignal signal, InsightAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var user = string.Format(
            CultureInfo.InvariantCulture,
            "Alert type: {0}\nDetails: {1}\n\nGive your root-cause hypothesis and one recommended action.",
            signal.TriggerType,
            signal.Subject);

        return new ChatCompletionRequest
        {
            Tenant = signal.Tenant,
            Model = options.Model,
            Temperature = options.Temperature,
            MaxTokens = options.MaxTokens,
            Messages =
            [
                new ChatMessage(ChatRole.System, options.SystemPrompt),
                new ChatMessage(ChatRole.User, user),
            ],
        };
    }
}
