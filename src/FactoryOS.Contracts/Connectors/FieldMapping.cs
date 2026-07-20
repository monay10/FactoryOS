namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// A single field-level mapping rule: how one canonical Standard Model field is produced from a source
/// record. Mapping is <b>data, not code</b> — these rules come from a manifest, never from a branch.
/// </summary>
public sealed record FieldMapping
{
    /// <summary>Gets the canonical Standard Model field this rule produces (for example <c>Quantity</c>).</summary>
    public required string Target { get; init; }

    /// <summary>Gets the source field to read the value from; ignored when <see cref="Constant"/> is set.</summary>
    public string? Source { get; init; }

    /// <summary>Gets the named value transform to apply (for example <c>decimal</c> or <c>upper</c>).</summary>
    public string? Transform { get; init; }

    /// <summary>Gets a constant value that overrides any source lookup, when present.</summary>
    public object? Constant { get; init; }

    /// <summary>Gets the value substituted when the source field is missing or null.</summary>
    public object? Default { get; init; }

    /// <summary>Gets a value indicating whether the mapping must yield a non-null value or fail.</summary>
    public bool Required { get; init; }
}
