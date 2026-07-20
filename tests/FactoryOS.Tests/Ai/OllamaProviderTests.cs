using System.Text.Json;
using FactoryOS.Ai.Providers;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Tests.Ai;

public sealed class OllamaProviderTests
{
    private static ChatCompletionRequest Request() => new()
    {
        Tenant = "acme",
        Model = "llama3",
        Messages = [new ChatMessage(ChatRole.User, "hi")],
        Temperature = 0.5m,
        MaxTokens = 32,
    };

    private static OllamaProvider Provider(StubHttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        return new OllamaProvider(client);
    }

    [Fact]
    public async Task Posts_ollama_shaped_request_and_normalizes_the_response()
    {
        const string body = """
        {
          "model": "llama3",
          "message": { "role": "assistant", "content": "merhaba" },
          "done": true,
          "done_reason": "stop",
          "prompt_eval_count": 8,
          "eval_count": 4
        }
        """;
        var handler = new StubHttpMessageHandler(body);
        var provider = Provider(handler);

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("merhaba", result.Value.Content);
        Assert.Equal("llama3", result.Value.Model);
        Assert.Equal("stop", result.Value.FinishReason);
        Assert.Equal(8, result.Value.PromptTokens);
        Assert.Equal(4, result.Value.CompletionTokens);

        Assert.Equal("/api/chat", handler.LastRequest!.RequestUri!.AbsolutePath);
        using var sent = JsonDocument.Parse(handler.LastRequestBody!);
        var root = sent.RootElement;
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(32, root.GetProperty("options").GetProperty("num_predict").GetInt32());
    }
}
