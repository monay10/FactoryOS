using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Providers;

/// <summary>
/// An <see cref="IEmbeddingProvider"/> for a local Ollama backend (<c>/api/embed</c>, batch input). Normalizes
/// Ollama's response dialect into the canonical <see cref="EmbeddingResponse"/>.
/// </summary>
public sealed class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="OllamaEmbeddingProvider"/> class.</summary>
    /// <param name="httpClient">The HTTP client, its base address pointing at the Ollama server.</param>
    public OllamaEmbeddingProvider(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public string Name => "ollama";

    /// <inheritdoc />
    public async Task<Result<EmbeddingResponse>> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = BuildBody(request);
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/embed") { Content = content };

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<EmbeddingResponse>(Error.Failure(
                    "Ai.Embedding.Ollama.HttpError",
                    $"Ollama backend returned {(int)response.StatusCode}."));
            }

            return Parse(payload);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<EmbeddingResponse>(Error.Failure(
                "Ai.Embedding.Ollama.Transport",
                $"Failed to reach the Ollama backend: {ex.Message}"));
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

            if (!root.TryGetProperty("embeddings", out var embeddings) ||
                embeddings.ValueKind != JsonValueKind.Array ||
                embeddings.GetArrayLength() == 0)
            {
                return Result.Failure<EmbeddingResponse>(Error.Failure(
                    "Ai.Embedding.Ollama.EmptyResponse", "The backend returned no embeddings."));
            }

            var vectors = new List<IReadOnlyList<float>>(embeddings.GetArrayLength());
            foreach (var embedding in embeddings.EnumerateArray())
            {
                if (embedding.ValueKind != JsonValueKind.Array)
                {
                    return Result.Failure<EmbeddingResponse>(Error.Failure(
                        "Ai.Embedding.Ollama.InvalidResponse", "An embedding entry was not a vector."));
                }

                vectors.Add(ReadVector(embedding));
            }

            var model = root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString() ?? string.Empty
                : string.Empty;

            var prompt = root.TryGetProperty("prompt_eval_count", out var p) && p.TryGetInt32(out var pv) ? pv : 0;

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
                "Ai.Embedding.Ollama.InvalidResponse", $"Could not parse the backend response: {ex.Message}"));
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
