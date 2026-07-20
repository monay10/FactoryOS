namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// A single raw record as read from an external system, before normalization. Its fields are the
/// vendor's own dialect (for example Logo column names); the connector never interprets them — mapping
/// to the Standard Model is data, applied downstream by the normalizer.
/// </summary>
/// <param name="SourceEntity">The source-side entity or table name (for example <c>LG_STLINE</c>).</param>
/// <param name="Fields">The raw field values, keyed by their source-side names.</param>
public sealed record SourceRecord(string SourceEntity, IReadOnlyDictionary<string, object?> Fields);
