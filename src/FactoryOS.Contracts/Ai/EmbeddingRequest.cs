namespace FactoryOS.Contracts.Ai;

/// <summary>
/// A provider-agnostic embedding request. The <see cref="Model"/> is a logical FactoryOS model key
/// (for example <c>embed</c> or <c>embed-local</c>); the embedding gateway resolves it to a concrete
/// provider and upstream model name. Every request carries its <see cref="Tenant"/> — AI is never
/// called out of scope.
/// </summary>
public sealed record EmbeddingRequest
{
    /// <summary>The tenant the request is made on behalf of.</summary>
    public required string Tenant { get; init; }

    /// <summary>The logical model key to route on.</summary>
    public required string Model { get; init; }

    /// <summary>The texts to embed; one output vector is produced per input, in order.</summary>
    public required IReadOnlyList<string> Inputs { get; init; }
}
