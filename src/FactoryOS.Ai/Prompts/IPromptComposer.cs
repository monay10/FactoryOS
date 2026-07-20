using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Prompts;

/// <summary>
/// Composes chat messages from a catalog prompt template and a set of variables — the bridge between the
/// prompt engine and the LLM Gateway.
/// </summary>
public interface IPromptComposer
{
    /// <summary>Renders the template registered under <paramref name="key"/> into chat messages.</summary>
    /// <param name="key">The logical template key.</param>
    /// <param name="variables">The variable values for placeholder substitution.</param>
    /// <returns>
    /// A successful result with the system (when present) and user messages, or a failure when the key is
    /// unknown or a variable is unbound.
    /// </returns>
    Result<IReadOnlyList<ChatMessage>> Compose(string key, IReadOnlyDictionary<string, string> variables);
}
