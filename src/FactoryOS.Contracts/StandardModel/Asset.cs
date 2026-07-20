namespace FactoryOS.Contracts.StandardModel;

/// <summary>
/// Canonical representation of a physical asset (a machine, line or piece of equipment) that modules
/// such as Maintenance and OEE reason about.
/// </summary>
public sealed record Asset : IStandardEntity
{
    /// <summary>The canonical entity type name.</summary>
    public const string Type = "Asset";

    /// <inheritdoc />
    public required string Tenant { get; init; }

    /// <summary>Gets the asset code; the natural key of the asset within a tenant.</summary>
    public required string Code { get; init; }

    /// <summary>Gets the human-readable asset name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the asset kind or category (for example <c>CNC</c> or <c>Compressor</c>).</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Gets the location the asset is installed at, if known.</summary>
    public string? Location { get; init; }

    /// <summary>Gets the operational status (for example <c>Running</c>, <c>Idle</c> or <c>Down</c>).</summary>
    public string Status { get; init; } = string.Empty;

    /// <inheritdoc />
    public string EntityType => Type;

    /// <inheritdoc />
    public string NaturalKey => Code;
}
