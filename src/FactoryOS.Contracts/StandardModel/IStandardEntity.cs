namespace FactoryOS.Contracts.StandardModel;

/// <summary>
/// The marker every canonical Standard Model entity implements. The Standard Model is the <b>only</b>
/// shared language on the event bus: connectors normalize every ERP/PLC dialect into these entities, so
/// no business module or agent ever speaks a vendor dialect. Every entity is tenant-scoped and carries a
/// stable natural key for idempotent, at-least-once delivery.
/// </summary>
public interface IStandardEntity
{
    /// <summary>Gets the tenant the entity belongs to. There is no cross-tenant Standard Model entity.</summary>
    string Tenant { get; }

    /// <summary>Gets the canonical entity type name (for example <c>InventoryItem</c>).</summary>
    string EntityType { get; }

    /// <summary>Gets the stable, tenant-unique natural key used to deduplicate and correlate the entity.</summary>
    string NaturalKey { get; }
}
