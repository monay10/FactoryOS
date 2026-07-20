namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// The declarative description of a connector (its <c>connector.json</c>). Like every plugin manifest,
/// it is data the core discovers and wires by contract — never by name.
/// </summary>
public sealed record ConnectorManifest
{
    /// <summary>Gets the stable, unique key that identifies the connector.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the human-readable connector name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the source system the connector reads (for example <c>Logo</c>).</summary>
    public required string SourceSystem { get; init; }

    /// <summary>Gets an optional human-readable description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the canonical Standard Model entity types this connector can produce.</summary>
    public IReadOnlyList<string> Provides { get; init; } = [];

    /// <summary>Gets the path, relative to the connector folder, of the mapping manifest that drives normalization.</summary>
    public string? Mapping { get; init; }
}
