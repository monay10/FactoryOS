using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Gateway;

/// <summary>
/// The LLM Gateway: the single entry point business modules and agents use to reach language models. It
/// resolves a logical model key to a configured provider and upstream model, so callers never bind to a
/// vendor. This is the AI analogue of the Connector layer being the only door to the outside.
/// </summary>
public interface ILlmGateway
{
    /// <summary>Routes a chat completion request to the provider configured for its logical model.</summary>
    /// <param name="request">The request; its <c>Model</c> is a logical FactoryOS model key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result with the completion, or a failure when the model is unknown or the backend fails.</returns>
    Task<Result<ChatCompletionResponse>> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken);
}
