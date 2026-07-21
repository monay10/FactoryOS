namespace FactoryOS.Plugins.Workflow.SLA.Domain;

/// <summary>A single, immutable entry in an SLA's audit history.</summary>
/// <param name="SlaId">The SLA the entry belongs to.</param>
/// <param name="Action">What happened.</param>
/// <param name="Actor">Who or what caused it (a user id, or the engine component).</param>
/// <param name="Detail">An optional human-readable detail.</param>
/// <param name="OccurredOnUtc">When it happened.</param>
public sealed record SlaHistoryEntry(
    Guid SlaId,
    SlaHistoryAction Action,
    string Actor,
    string? Detail,
    DateTimeOffset OccurredOnUtc);
