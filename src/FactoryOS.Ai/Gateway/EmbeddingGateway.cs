using FactoryOS.Ai.Configuration;
using FactoryOS.Ai.Providers;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;
using Microsoft.Extensions.Options;

namespace FactoryOS.Ai.Gateway;

/// <summary>
/// The default <see cref="IEmbeddingGateway"/>. It looks up the logical model in the routing table, selects
/// the named provider, rewrites the request to the upstream model name and delegates. Unknown models and
/// unknown providers fail fast rather than silently falling back.
/// </summary>
public sealed class EmbeddingGateway : IEmbeddingGateway
{
    private readonly Dictionary<string, IEmbeddingProvider> _providers;
    private readonly EmbeddingGatewayOptions _options;

    /// <summary>Initializes a new instance of the <see cref="EmbeddingGateway"/> class.</summary>
    /// <param name="providers">The available providers, indexed by their <see cref="IEmbeddingProvider.Name"/>.</param>
    /// <param name="options">The routing options.</param>
    public EmbeddingGateway(IEnumerable<IEmbeddingProvider> providers, IOptions<EmbeddingGatewayOptions> options)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);

        var map = new Dictionary<string, IEmbeddingProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            map[provider.Name] = provider;
        }

        _providers = map;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<Result<EmbeddingResponse>> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Models.TryGetValue(request.Model, out var route))
        {
            return Task.FromResult(Result.Failure<EmbeddingResponse>(Error.NotFound(
                "Ai.Embedding.UnknownModel",
                $"No route is configured for logical embedding model '{request.Model}'.")));
        }

        if (!_providers.TryGetValue(route.Provider, out var provider))
        {
            return Task.FromResult(Result.Failure<EmbeddingResponse>(Error.NotFound(
                "Ai.Embedding.UnknownProvider",
                $"Model '{request.Model}' routes to provider '{route.Provider}', which is not registered.")));
        }

        var upstream = request with { Model = route.UpstreamModel };
        return provider.EmbedAsync(upstream, cancellationToken);
    }
}
