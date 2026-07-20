namespace FactoryOS.Contracts.Ai;

/// <summary>
/// A provider-agnostic chat completion request. The <see cref="Model"/> is a logical FactoryOS model key
/// (for example <c>fast</c> or <c>reasoning</c>); the LLM Gateway resolves it to a concrete provider and
/// upstream model name. Every request carries its <see cref="Tenant"/> — AI is never called out of scope.
/// </summary>
public sealed record ChatCompletionRequest
{
    /// <summary>The tenant the request is made on behalf of.</summary>
    public required string Tenant { get; init; }

    /// <summary>The logical model key to route on.</summary>
    public required string Model { get; init; }

    /// <summary>The conversation so far.</summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>Optional sampling temperature; provider default when null.</summary>
    public decimal? Temperature { get; init; }

    /// <summary>Optional cap on generated tokens; provider default when null.</summary>
    public int? MaxTokens { get; init; }
}
