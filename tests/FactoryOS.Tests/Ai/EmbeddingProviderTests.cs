using System.Text.Json;
using FactoryOS.Ai.Configuration;
using FactoryOS.Ai.Providers;
using FactoryOS.Contracts.Ai;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Ai;

public sealed class EmbeddingProviderTests
{
    private static EmbeddingRequest Request(params string[] inputs) => new()
    {
        Tenant = "acme",
        Model = "text-embedding-3-small",
        Inputs = inputs,
    };

    [Fact]
    public async Task OpenAi_posts_to_the_embeddings_endpoint_with_the_api_key_and_input_array()
    {
        const string body = """
        { "model": "text-embedding-3-small",
          "data": [ { "index": 0, "embedding": [0.1, 0.2, 0.3] } ],
          "usage": { "prompt_tokens": 7 } }
        """;
        using var handler = new StubHttpMessageHandler(body);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com") };
        var options = Options.Create(new OpenAiProviderOptions { ApiKey = "sk-test" });
        var provider = new OpenAiEmbeddingProvider(client, options);

        var result = await provider.EmbedAsync(Request("hello"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("/v1/embeddings", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test", handler.LastRequest.Headers.Authorization.Parameter);

        using var sent = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("text-embedding-3-small", sent.RootElement.GetProperty("model").GetString());
        Assert.Equal("hello", sent.RootElement.GetProperty("input")[0].GetString());
    }

    [Fact]
    public async Task OpenAi_normalizes_vectors_usage_and_dimensions()
    {
        const string body = """
        { "model": "text-embedding-3-small",
          "data": [
            { "index": 0, "embedding": [1.0, 0.0] },
            { "index": 1, "embedding": [0.0, 1.0] }
          ],
          "usage": { "prompt_tokens": 12 } }
        """;
        using var handler = new StubHttpMessageHandler(body);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com") };
        var provider = new OpenAiEmbeddingProvider(client, Options.Create(new OpenAiProviderOptions()));

        var result = await provider.EmbedAsync(Request("a", "b"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(2, result.Value.Vectors.Count);
        Assert.Equal(2, result.Value.Dimensions);
        Assert.Equal(12, result.Value.PromptTokens);
        Assert.Equal([1.0f, 0.0f], result.Value.Vectors[0]);
    }

    [Fact]
    public async Task OpenAi_fails_on_a_non_success_status()
    {
        using var handler = new StubHttpMessageHandler("nope", System.Net.HttpStatusCode.InternalServerError);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com") };
        var provider = new OpenAiEmbeddingProvider(client, Options.Create(new OpenAiProviderOptions()));

        var result = await provider.EmbedAsync(Request("x"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Embedding.OpenAi.HttpError", result.Error.Code);
    }

    [Fact]
    public async Task Ollama_posts_to_api_embed_and_normalizes_the_embeddings_array()
    {
        const string body = """
        { "model": "nomic-embed-text",
          "embeddings": [ [0.5, 0.5, 0.5] ],
          "prompt_eval_count": 4 }
        """;
        using var handler = new StubHttpMessageHandler(body);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var provider = new OllamaEmbeddingProvider(client);

        var result = await provider.EmbedAsync(Request("hello"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("/api/embed", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Single(result.Value.Vectors);
        Assert.Equal(3, result.Value.Dimensions);
        Assert.Equal(4, result.Value.PromptTokens);
    }

    [Fact]
    public async Task Ollama_fails_when_no_embeddings_are_returned()
    {
        using var handler = new StubHttpMessageHandler("""{ "model": "nomic-embed-text", "embeddings": [] }""");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var provider = new OllamaEmbeddingProvider(client);

        var result = await provider.EmbedAsync(Request("x"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Embedding.Ollama.EmptyResponse", result.Error.Code);
    }
}
