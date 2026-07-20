using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Providers;

/// <summary>
/// A single embedding backend behind the gateway. Each provider speaks HTTP to its vendor and normalizes the
/// vendor dialect into the canonical <see cref="EmbeddingResponse"/>. Callers never see a provider directly.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>The provider key used by the gateway routing table (for example <c>openai</c> or <c>ollama</c>).</summary>
    string Name { get; }

    /// <summary>Embeds the request inputs against the (already upstream-resolved) model.</summary>
    /// <param name="request">The request; its <c>Model</c> is the concrete upstream model name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result with one vector per input, or a failure when the backend fails.</returns>
    Task<Result<EmbeddingResponse>> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken);
}
