using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Providers;

/// <summary>
/// A single upstream LLM backend (OpenAI-compatible, Ollama, …). Providers speak HTTP to their backend and
/// normalize the response into the canonical <see cref="ChatCompletionResponse"/>. AI is always called over
/// HTTP, never in-process (Constitution / locked stack).
/// </summary>
public interface ILlmProvider
{
    /// <summary>The provider key the gateway routes on (for example <c>openai</c> or <c>ollama</c>).</summary>
    string Name { get; }

    /// <summary>Requests a chat completion from the backend.</summary>
    /// <param name="request">The completion request; its <c>Model</c> is the upstream model name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result with the completion, or a failure describing the transport/backend error.</returns>
    Task<Result<ChatCompletionResponse>> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken);
}
