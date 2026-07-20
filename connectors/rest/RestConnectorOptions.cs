namespace FactoryOS.Connectors.Rest;

/// <summary>
/// Strongly-typed configuration for the REST connector: which resource to GET, where the record array
/// lives in the response, and the source-entity name its objects are tagged with.
/// </summary>
public sealed record RestConnectorOptions
{
    /// <summary>Gets the resource path (relative to the client's base address) to GET.</summary>
    public required string RequestPath { get; init; }

    /// <summary>Gets the source-entity name assigned to every object (for example <c>Product</c>).</summary>
    public required string SourceEntity { get; init; }

    /// <summary>
    /// Gets the dot-separated path to the JSON array of records within the response (for example
    /// <c>data.items</c>). Empty means the response body is itself the array.
    /// </summary>
    public string ArrayPath { get; init; } = string.Empty;
}
