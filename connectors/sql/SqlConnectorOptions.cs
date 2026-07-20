namespace FactoryOS.Connectors.Sql;

/// <summary>
/// Strongly-typed configuration for the SQL connector: the query to run and the source-entity name its
/// rows are tagged with. A new SQL source is configuration only.
/// </summary>
public sealed record SqlConnectorOptions
{
    /// <summary>Gets the SQL query whose result set is read row by row.</summary>
    public required string Query { get; init; }

    /// <summary>Gets the source-entity name assigned to every row (for example <c>LG_STLINE</c>).</summary>
    public required string SourceEntity { get; init; }
}
