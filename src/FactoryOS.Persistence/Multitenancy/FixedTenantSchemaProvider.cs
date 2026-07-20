namespace FactoryOS.Persistence.Multitenancy;

/// <summary>An <see cref="ITenantSchemaProvider"/> bound to a single, fixed schema.</summary>
public sealed class FixedTenantSchemaProvider : ITenantSchemaProvider
{
    /// <summary>Initializes a new instance of the <see cref="FixedTenantSchemaProvider"/> class.</summary>
    /// <param name="schema">The fixed schema name.</param>
    public FixedTenantSchemaProvider(string schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        Schema = schema;
    }

    /// <inheritdoc />
    public string Schema { get; }
}
