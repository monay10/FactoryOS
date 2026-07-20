namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// A record after normalization into the Standard Model: its values are keyed by canonical field names
/// and it carries the tenant, source system, canonical entity type and natural key. This is the wire
/// form of a Standard Model entity that flows on the event bus; a typed entity can be bound from it.
/// </summary>
/// <param name="Tenant">The tenant the record belongs to.</param>
/// <param name="SourceSystem">The source system the record originated from (for example <c>Logo</c>).</param>
/// <param name="EntityType">The canonical Standard Model entity type (for example <c>InventoryItem</c>).</param>
/// <param name="NaturalKey">The stable, tenant-unique natural key used for deduplication.</param>
/// <param name="Values">The canonical field values, keyed by Standard Model field names.</param>
public sealed record NormalizedRecord(
    string Tenant,
    string SourceSystem,
    string EntityType,
    string NaturalKey,
    IReadOnlyDictionary<string, object?> Values);
