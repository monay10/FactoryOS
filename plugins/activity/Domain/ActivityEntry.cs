namespace FactoryOS.Plugins.Activity.Domain;

/// <summary>
/// One normalized line in a tenant's activity feed — the uniform, human-readable form of any noteworthy event
/// the module observes. Each event handler maps its specific integration event into this shape, so the feed
/// (and anything that later reads or indexes it) works against one structure regardless of origin.
/// </summary>
/// <param name="Tenant">The tenant the activity belongs to.</param>
/// <param name="Category">The coarse origin bucket (for example <c>Rule</c>, <c>Maintenance</c>, <c>Safety</c>).</param>
/// <param name="Headline">A concise human-readable description of what happened.</param>
/// <param name="OccurredAt">When the underlying event occurred.</param>
/// <param name="SourceEventId">The producing event's id — the feed's dedupe key and traceability anchor.</param>
public readonly record struct ActivityEntry(
    string Tenant,
    string Category,
    string Headline,
    DateTimeOffset OccurredAt,
    Guid SourceEventId);
