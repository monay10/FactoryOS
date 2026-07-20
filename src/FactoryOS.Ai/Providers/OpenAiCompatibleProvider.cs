using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactoryOS.Ai.Configuration;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;
using Microsoft.Extensions.Options;

namespace FactoryOS.Ai.Providers;

/// <summary>
/// An <see cref="ILlmProvider"/> for any OpenAI-compatible <c>/v1/chat/completions</c> backend (OpenAI,
/// Azure OpenAI, vLLM, LM Studio, …). Normalizes the OpenAI response dialect into the canonical
/// <see cref="ChatCompletionResponse"/>.
/// </summary>
public sealed class OpenAiCompatibleProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    /// <summary>Initializes a new instance of the <see cref="OpenAiCompatibleProvider"/> class.</summary>
    /// <param name="httpClient">The HTTP client, its base address pointing at the backend.</param>
    /// <param name="options">The provider connection options.</param>
    public OpenAiCompatibleProvider(HttpClient httpClient, IOptions<OpenAiProviderOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _apiKey = options.Value.ApiKey;
    }

    /// <inheritdoc />
    public string Name => "openai";

    /// <inheritdoc />
    public async Task<Result<ChatCompletionResponse>> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = BuildBody(request);
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions") { Content = content };
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<ChatCompletionResponse>(Error.Failure(
                    "Ai.Llm.OpenAi.HttpError",
                    $"OpenAI-compatible backend returned {(int)response.StatusCode}."));
            }

            return Parse(payload);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<ChatCompletionResponse>(Error.Failure(
                "Ai.Llm.OpenAi.Transport",
                $"Failed to reach the OpenAI-compatible backend: {ex.Message}"));
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

        if (request.Temperature is { } temperature)
        {
            body["temperature"] = JsonValue.Create(temperature);
        }

        if (request.MaxTokens is { } maxTokens)
        {
            body["max_tokens"] = maxTokens;
        }

        return body;
    }

    private static Result<ChatCompletionResponse> Parse(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return Result.Failure<ChatCompletionResponse>(Error.Failure(
                    "Ai.Llm.OpenAi.EmptyResponse", "The backend returned no choices."));
            }

            var first = choices[0];
            var text = first.TryGetProperty("message", out var messageElement) &&
                       messageElement.TryGetProperty("content", out var contentElement)
                ? contentElement.GetString() ?? string.Empty
                : string.Empty;

            var finishReason = first.TryGetProperty("finish_reason", out var finish)
                ? finish.GetString()
                : null;

            var model = root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString() ?? string.Empty
                : string.Empty;

            var (prompt, completion) = ReadUsage(root);

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
                "Ai.Llm.OpenAi.InvalidResponse", $"Could not parse the backend response: {ex.Message}"));
        }
    }

    private static (int Prompt, int Completion) ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return (0, 0);
        }

        var prompt = usage.TryGetProperty("prompt_tokens", out var p) && p.TryGetInt32(out var pv) ? pv : 0;
        var completion = usage.TryGetProperty("completion_tokens", out var c) && c.TryGetInt32(out var cv) ? cv : 0;
        return (prompt, completion);
    }
}
