namespace FactoryOS.Persistence.Multitenancy;

/// <summary>
/// Supplies the database schema a tenant's data lives in. FactoryOS isolates every tenant in its own
/// PostgreSQL schema, so the schema name is resolved per request and applied as the model's default.
/// </summary>
public interface ITenantSchemaProvider
{
    /// <summary>Gets the current tenant's schema name.</summary>
    string Schema { get; }
}
