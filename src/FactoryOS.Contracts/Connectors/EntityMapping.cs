namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// Maps one source-side entity to a canonical Standard Model entity: which target entity it becomes,
/// which target fields form its natural key, and the field-level rules that build it.
/// </summary>
public sealed record EntityMapping
{
    /// <summary>Gets the source-side entity or table name this mapping applies to (for example <c>LG_STLINE</c>).</summary>
    public required string SourceEntity { get; init; }

    /// <summary>Gets the canonical Standard Model entity type produced (for example <c>InventoryItem</c>).</summary>
    public required string TargetEntity { get; init; }

    /// <summary>Gets the target fields whose values, joined, form the record's natural key.</summary>
    public required IReadOnlyList<string> NaturalKey { get; init; }

    /// <summary>Gets the field-level mapping rules.</summary>
    public required IReadOnlyList<FieldMapping> Fields { get; init; }
}
