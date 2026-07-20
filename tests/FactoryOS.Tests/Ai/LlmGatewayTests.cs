using FactoryOS.Ai.Configuration;
using FactoryOS.Ai.Gateway;
using FactoryOS.Ai.Providers;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Ai;

public sealed class LlmGatewayTests
{
    private sealed class RecordingProvider(string name) : ILlmProvider
    {
        public string Name => name;

        public ChatCompletionRequest? Received { get; private set; }

        public Task<Result<ChatCompletionResponse>> CompleteAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken)
        {
            Received = request;
            return Task.FromResult(Result.Success(new ChatCompletionResponse
            {
                Model = request.Model,
                Content = $"served by {name}",
            }));
        }
    }

    private static ChatCompletionRequest Request(string model) => new()
    {
        Tenant = "acme",
        Model = model,
        Messages = [new ChatMessage(ChatRole.User, "hi")],
    };

    private static LlmGateway Gateway(IEnumerable<ILlmProvider> providers, LlmGatewayOptions options)
        => new(providers, Options.Create(options));

    [Fact]
    public async Task Routes_to_the_configured_provider_and_rewrites_to_the_upstream_model()
    {
        var openai = new RecordingProvider("openai");
        var options = new LlmGatewayOptions();
        options.Models["fast"] = new LlmModelRoute { Provider = "openai", UpstreamModel = "gpt-4o-mini" };
        var gateway = Gateway([openai], options);

        var result = await gateway.CompleteAsync(Request("fast"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("served by openai", result.Value.Content);
        Assert.Equal("gpt-4o-mini", openai.Received!.Model); // logical "fast" rewritten to upstream name
        Assert.Equal("acme", openai.Received.Tenant);
    }

    [Fact]
    public async Task Fails_when_the_logical_model_is_not_routed()
    {
        var gateway = Gateway([new RecordingProvider("openai")], new LlmGatewayOptions());

        var result = await gateway.CompleteAsync(Request("unknown"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Llm.UnknownModel", result.Error.Code);
    }

    [Fact]
    public async Task Fails_when_the_routed_provider_is_not_registered()
    {
        var options = new LlmGatewayOptions();
        options.Models["local"] = new LlmModelRoute { Provider = "ollama", UpstreamModel = "llama3" };
        var gateway = Gateway([new RecordingProvider("openai")], options);

        var result = await gateway.CompleteAsync(Request("local"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Llm.UnknownProvider", result.Error.Code);
    }

    [Fact]
    public async Task Selects_among_multiple_providers_by_route()
    {
        var openai = new RecordingProvider("openai");
        var ollama = new RecordingProvider("ollama");
        var options = new LlmGatewayOptions();
        options.Models["fast"] = new LlmModelRoute { Provider = "openai", UpstreamModel = "gpt-4o-mini" };
        options.Models["local"] = new LlmModelRoute { Provider = "ollama", UpstreamModel = "llama3" };
        var gateway = Gateway([openai, ollama], options);

        await gateway.CompleteAsync(Request("local"), CancellationToken.None);

        Assert.Equal("llama3", ollama.Received!.Model);
        Assert.Null(openai.Received); // untouched
    }
}
