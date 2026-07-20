namespace FactoryOS.Contracts.StandardModel;

/// <summary>
/// Canonical representation of a work order (a unit of planned or corrective work), shared by the
/// Maintenance, Production and Workflow modules.
/// </summary>
public sealed record WorkOrder : IStandardEntity
{
    /// <summary>The canonical entity type name.</summary>
    public const string Type = "WorkOrder";

    /// <inheritdoc />
    public required string Tenant { get; init; }

    /// <summary>Gets the work-order number; the natural key within a tenant.</summary>
    public required string Number { get; init; }

    /// <summary>Gets the work-order title or summary.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the current status (for example <c>Open</c>, <c>InProgress</c> or <c>Closed</c>).</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets the code of the asset the work order targets, if any.</summary>
    public string? AssetCode { get; init; }

    /// <summary>Gets the instant the work order is due, if scheduled.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <inheritdoc />
    public string EntityType => Type;

    /// <inheritdoc />
    public string NaturalKey => Number;
}
