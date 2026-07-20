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
/// An <see cref="IEmbeddingProvider"/> for any OpenAI-compatible <c>/v1/embeddings</c> backend (OpenAI,
/// Azure OpenAI, vLLM, …). Normalizes the OpenAI response dialect into the canonical
/// <see cref="EmbeddingResponse"/>.
/// </summary>
public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    /// <summary>Initializes a new instance of the <see cref="OpenAiEmbeddingProvider"/> class.</summary>
    /// <param name="httpClient">The HTTP client, its base address pointing at the backend.</param>
    /// <param name="options">The provider connection options.</param>
    public OpenAiEmbeddingProvider(HttpClient httpClient, IOptions<OpenAiProviderOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _apiKey = options.Value.ApiKey;
    }

    /// <inheritdoc />
    public string Name => "openai";

    /// <inheritdoc />
    public async Task<Result<EmbeddingResponse>> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = BuildBody(request);
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/embeddings") { Content = content };
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
                return Result.Failure<EmbeddingResponse>(Error.Failure(
                    "Ai.Embedding.OpenAi.HttpError",
                    $"OpenAI-compatible backend returned {(int)response.StatusCode}."));
            }

            return Parse(payload);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<EmbeddingResponse>(Error.Failure(
                "Ai.Embedding.OpenAi.Transport",
                $"Failed to reach the OpenAI-compatible backend: {ex.Message}"));
        }
    }

    private static JsonObject BuildBody(EmbeddingRequest request)
    {
        var inputs = new JsonArray();
        foreach (var input in request.Inputs)
        {
            inputs.Add(input);
        }

        return new JsonObject
        {
            ["model"] = request.Model,
            ["input"] = inputs,
        };
    }

    private static Result<EmbeddingResponse> Parse(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (!root.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array ||
                data.GetArrayLength() == 0)
            {
                return Result.Failure<EmbeddingResponse>(Error.Failure(
                    "Ai.Embedding.OpenAi.EmptyResponse", "The backend returned no embeddings."));
            }

            var vectors = new List<IReadOnlyList<float>>(data.GetArrayLength());
            foreach (var item in data.EnumerateArray())
            {
                if (!item.TryGetProperty("embedding", out var embedding) || embedding.ValueKind != JsonValueKind.Array)
                {
                    return Result.Failure<EmbeddingResponse>(Error.Failure(
                        "Ai.Embedding.OpenAi.InvalidResponse", "An embedding entry had no vector."));
                }

                vectors.Add(ReadVector(embedding));
            }

            var model = root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString() ?? string.Empty
                : string.Empty;

            var prompt = root.TryGetProperty("usage", out var usage) &&
                         usage.ValueKind == JsonValueKind.Object &&
                         usage.TryGetProperty("prompt_tokens", out var p) &&
                         p.TryGetInt32(out var pv)
                ? pv
                : 0;

            return Result.Success(new EmbeddingResponse
            {
                Model = model,
                Vectors = vectors,
                PromptTokens = prompt,
            });
        }
        catch (JsonException ex)
        {
            return Result.Failure<EmbeddingResponse>(Error.Failure(
                "Ai.Embedding.OpenAi.InvalidResponse", $"Could not parse the backend response: {ex.Message}"));
        }
    }

    private static float[] ReadVector(JsonElement array)
    {
        var vector = new float[array.GetArrayLength()];
        var index = 0;
        foreach (var component in array.EnumerateArray())
        {
            vector[index++] = component.GetSingle();
        }

        return vector;
    }
}
