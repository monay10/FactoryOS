using System.Net;
using System.Text.Json;
using FactoryOS.Ai.Configuration;
using FactoryOS.Ai.Providers;
using FactoryOS.Contracts.Ai;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Ai;

public sealed class OpenAiCompatibleProviderTests
{
    private static ChatCompletionRequest Request() => new()
    {
        Tenant = "acme",
        Model = "gpt-4o-mini",
        Messages = [new ChatMessage(ChatRole.System, "be terse"), new ChatMessage(ChatRole.User, "hi")],
        Temperature = 0.2m,
        MaxTokens = 64,
    };

    private static OpenAiCompatibleProvider Provider(StubHttpMessageHandler handler, string apiKey = "sk-test")
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.test") };
        return new OpenAiCompatibleProvider(client, Options.Create(new OpenAiProviderOptions { ApiKey = apiKey }));
    }

    [Fact]
    public async Task Posts_openai_shaped_request_and_normalizes_the_response()
    {
        const string body = """
        {
          "model": "gpt-4o-mini",
          "choices": [{ "message": { "role": "assistant", "content": "hello" }, "finish_reason": "stop" }],
          "usage": { "prompt_tokens": 12, "completion_tokens": 3 }
        }
        """;
        var handler = new StubHttpMessageHandler(body);
        var provider = Provider(handler);

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("hello", result.Value.Content);
        Assert.Equal("gpt-4o-mini", result.Value.Model);
        Assert.Equal("stop", result.Value.FinishReason);
        Assert.Equal(12, result.Value.PromptTokens);
        Assert.Equal(3, result.Value.CompletionTokens);
        Assert.Equal(15, result.Value.TotalTokens);

        // Verify the request the provider actually sent.
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/v1/chat/completions", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test", handler.LastRequest.Headers.Authorization.Parameter);

        using var sent = JsonDocument.Parse(handler.LastRequestBody!);
        var root = sent.RootElement;
        Assert.Equal("gpt-4o-mini", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("hi", root.GetProperty("messages")[1].GetProperty("content").GetString());
        Assert.Equal(64, root.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task Maps_a_non_success_status_to_a_failure()
    {
        var handler = new StubHttpMessageHandler("nope", HttpStatusCode.TooManyRequests);
        var provider = Provider(handler);

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Llm.OpenAi.HttpError", result.Error.Code);
    }

    [Fact]
    public async Task Omits_the_authorization_header_when_no_key_is_configured()
    {
        var handler = new StubHttpMessageHandler(
            """{ "model": "m", "choices": [{ "message": { "content": "x" } }] }""");
        var provider = Provider(handler, apiKey: "");

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Null(handler.LastRequest!.Headers.Authorization);
    }
}
