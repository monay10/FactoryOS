namespace FactoryOS.Connectors.Csv;

/// <summary>
/// Strongly-typed configuration for the CSV connector. A new CSV source is configuration only: a file
/// path, a delimiter and the source-entity name the rows are tagged with.
/// </summary>
public sealed record CsvConnectorOptions
{
    /// <summary>Gets the full path to the CSV file to read.</summary>
    public required string FilePath { get; init; }

    /// <summary>Gets the source-entity name assigned to every row (for example <c>Inventory</c>).</summary>
    public required string SourceEntity { get; init; }

    /// <summary>Gets the field delimiter; defaults to a comma.</summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>Gets a value indicating whether the first row holds column headers used as field names.</summary>
    public bool HasHeader { get; init; } = true;
}
