using FactoryOS.Ai.Configuration;
using FactoryOS.Ai.Providers;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;
using Microsoft.Extensions.Options;

namespace FactoryOS.Ai.Gateway;

/// <summary>
/// The default <see cref="ILlmGateway"/>. It looks up the logical model in the routing table, selects the
/// named provider, rewrites the request to the upstream model name and delegates. Unknown models and
/// unknown providers fail fast rather than silently falling back.
/// </summary>
public sealed class LlmGateway : ILlmGateway
{
    private readonly Dictionary<string, ILlmProvider> _providers;
    private readonly LlmGatewayOptions _options;

    /// <summary>Initializes a new instance of the <see cref="LlmGateway"/> class.</summary>
    /// <param name="providers">The available providers, indexed by their <see cref="ILlmProvider.Name"/>.</param>
    /// <param name="options">The routing options.</param>
    public LlmGateway(IEnumerable<ILlmProvider> providers, IOptions<LlmGatewayOptions> options)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);

        var map = new Dictionary<string, ILlmProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            map[provider.Name] = provider;
        }

        _providers = map;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<Result<ChatCompletionResponse>> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Models.TryGetValue(request.Model, out var route))
        {
            return Task.FromResult(Result.Failure<ChatCompletionResponse>(Error.NotFound(
                "Ai.Llm.UnknownModel",
                $"No route is configured for logical model '{request.Model}'.")));
        }

        if (!_providers.TryGetValue(route.Provider, out var provider))
        {
            return Task.FromResult(Result.Failure<ChatCompletionResponse>(Error.NotFound(
                "Ai.Llm.UnknownProvider",
                $"Model '{request.Model}' routes to provider '{route.Provider}', which is not registered.")));
        }

        var upstream = request with { Model = route.UpstreamModel };
        return provider.CompleteAsync(upstream, cancellationToken);
    }
}
