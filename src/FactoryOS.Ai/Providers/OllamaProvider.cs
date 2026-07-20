using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Providers;

/// <summary>
/// An <see cref="ILlmProvider"/> for a local Ollama backend (<c>/api/chat</c>, non-streaming). Normalizes
/// Ollama's response dialect into the canonical <see cref="ChatCompletionResponse"/>.
/// </summary>
public sealed class OllamaProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="OllamaProvider"/> class.</summary>
    /// <param name="httpClient">The HTTP client, its base address pointing at the Ollama server.</param>
    public OllamaProvider(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public string Name => "ollama";

    /// <inheritdoc />
    public async Task<Result<ChatCompletionResponse>> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = BuildBody(request);
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat") { Content = content };

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<ChatCompletionResponse>(Error.Failure(
                    "Ai.Llm.Ollama.HttpError",
                    $"Ollama backend returned {(int)response.StatusCode}."));
            }

            return Parse(payload);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<ChatCompletionResponse>(Error.Failure(
                "Ai.Llm.Ollama.Transport",
                $"Failed to reach the Ollama backend: {ex.Message}"));
        }
    }

    private static JsonObject BuildBody(ChatCompletionRequest request)
    {
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            messages.Add(new JsonObject
            {
                ["role"] = ChatRoleNames.ToWire(message.Role),
                ["content"] = message.Content,
            });
        }

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["stream"] = false,
        };

        var options = new JsonObject();
        if (request.Temperature is { } temperature)
        {
            options["temperature"] = JsonValue.Create(temperature);
        }

        if (request.MaxTokens is { } maxTokens)
        {
            options["num_predict"] = maxTokens;
        }

        if (options.Count > 0)
        {
            body["options"] = options;
        }

        return body;
    }

    private static Result<ChatCompletionResponse> Parse(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var text = root.TryGetProperty("message", out var messageElement) &&
                       messageElement.TryGetProperty("content", out var contentElement)
                ? contentElement.GetString() ?? string.Empty
                : string.Empty;

            var finishReason = root.TryGetProperty("done_reason", out var done)
                ? done.GetString()
                : null;

            var model = root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString() ?? string.Empty
                : string.Empty;

            var prompt = root.TryGetProperty("prompt_eval_count", out var p) && p.TryGetInt32(out var pv) ? pv : 0;
            var completion = root.TryGetProperty("eval_count", out var c) && c.TryGetInt32(out var cv) ? cv : 0;

            return Result.Success(new ChatCompletionResponse
            {
                Model = model,
                Content = text,
                FinishReason = finishReason,
                PromptTokens = prompt,
                CompletionTokens = completion,
            });
        }
        catch (JsonException ex)
        {
            return Result.Failure<ChatCompletionResponse>(Error.Failure(
                "Ai.Llm.Ollama.InvalidResponse", $"Could not parse the backend response: {ex.Message}"));
        }
    }
}
