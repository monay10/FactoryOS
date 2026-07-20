namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a report artifact was rendered and stored. Notification, dashboards and audit consume it
/// to surface or link the artifact, without referencing the Reporting module or the object store. The bytes live
/// in the object store under <see cref="ObjectKey"/>; this event carries only the reference.
/// </summary>
public sealed record ReportGenerated : IntegrationEvent
{
    /// <summary>The tenant the report belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The report's stable id (for example the schedule id that triggered it).</summary>
    public required string ReportId { get; init; }

    /// <summary>The object-store key the rendered artifact was stored under.</summary>
    public required string ObjectKey { get; init; }

    /// <summary>The artifact's MIME content type (for example <c>text/csv</c>).</summary>
    public required string ContentType { get; init; }

    /// <summary>The artifact's size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>The number of data rows the report contains (excluding the header).</summary>
    public int RowCount { get; init; }

    /// <summary>The instant the report was generated (the triggering event's instant).</summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>The id of the event that triggered generation, for idempotent consumers.</summary>
    public Guid SourceEventId { get; init; }
}
