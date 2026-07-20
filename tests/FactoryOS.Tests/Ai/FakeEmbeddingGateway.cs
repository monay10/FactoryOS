using FactoryOS.Ai.Gateway;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Tests.Ai;

/// <summary>
/// A deterministic in-test <see cref="IEmbeddingGateway"/>. It embeds each input with a caller-supplied
/// function, so knowledge-base indexing and retrieval can be exercised offline without a real model.
/// </summary>
internal sealed class FakeEmbeddingGateway(Func<string, float[]> embed) : IEmbeddingGateway
{
    public Task<Result<EmbeddingResponse>> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken)
    {
        var vectors = request.Inputs.Select(input => (IReadOnlyList<float>)embed(input)).ToList();
        return Task.FromResult(Result.Success(new EmbeddingResponse
        {
            Model = request.Model,
            Vectors = vectors,
        }));
    }

    /// <summary>A keyword-presence embedding: one axis per keyword, 1 when present else 0.</summary>
    public static float[] KeywordEmbed(string text) =>
    [
        text.Contains("pump", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
        text.Contains("boiler", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
        text.Contains("valve", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
    ];
}
