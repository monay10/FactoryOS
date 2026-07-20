using FactoryOS.Ai.Configuration;
using FactoryOS.Ai.Gateway;
using FactoryOS.Ai.Providers;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Ai;

public sealed class EmbeddingGatewayTests
{
    private sealed class RecordingProvider(string name) : IEmbeddingProvider
    {
        public string Name => name;

        public EmbeddingRequest? Received { get; private set; }

        public Task<Result<EmbeddingResponse>> EmbedAsync(
            EmbeddingRequest request,
            CancellationToken cancellationToken)
        {
            Received = request;
            return Task.FromResult(Result.Success(new EmbeddingResponse
            {
                Model = request.Model,
                Vectors = [[0.1f, 0.2f]],
            }));
        }
    }

    private static EmbeddingRequest Request(string model) => new()
    {
        Tenant = "acme",
        Model = model,
        Inputs = ["hello"],
    };

    private static EmbeddingGateway Gateway(IEnumerable<IEmbeddingProvider> providers, EmbeddingGatewayOptions options)
        => new(providers, Options.Create(options));

    [Fact]
    public async Task Routes_to_the_configured_provider_and_rewrites_to_the_upstream_model()
    {
        var openai = new RecordingProvider("openai");
        var options = new EmbeddingGatewayOptions();
        options.Models["embed"] = new EmbeddingModelRoute { Provider = "openai", UpstreamModel = "text-embedding-3-small" };
        var gateway = Gateway([openai], options);

        var result = await gateway.EmbedAsync(Request("embed"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("text-embedding-3-small", openai.Received!.Model);
        Assert.Equal("acme", openai.Received.Tenant);
    }

    [Fact]
    public async Task Fails_when_the_logical_model_is_not_routed()
    {
        var gateway = Gateway([new RecordingProvider("openai")], new EmbeddingGatewayOptions());

        var result = await gateway.EmbedAsync(Request("unknown"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Embedding.UnknownModel", result.Error.Code);
    }

    [Fact]
    public async Task Fails_when_the_routed_provider_is_not_registered()
    {
        var options = new EmbeddingGatewayOptions();
        options.Models["embed-local"] = new EmbeddingModelRoute { Provider = "ollama", UpstreamModel = "nomic-embed-text" };
        var gateway = Gateway([new RecordingProvider("openai")], options);

        var result = await gateway.EmbedAsync(Request("embed-local"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Embedding.UnknownProvider", result.Error.Code);
    }
}
