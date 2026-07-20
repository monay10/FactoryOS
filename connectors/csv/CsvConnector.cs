using System.Globalization;
using System.Runtime.CompilerServices;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Csv;

/// <summary>
/// A generic connector that reads a delimited text file and yields one <see cref="SourceRecord"/> per
/// row. Field names come from the header row, or are positional (<c>col0</c>, <c>col1</c>, …) when the
/// file has none. Interpretation is left to the mapping-driven normalizer.
/// </summary>
public sealed class CsvConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "csv";

    private readonly CsvConnectorOptions _options;

    /// <summary>Initializes a new instance of the <see cref="CsvConnector"/> class.</summary>
    /// <param name="options">The CSV source configuration.</param>
    public CsvConnector(CsvConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SourceEntity);
        _options = options;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "CSV";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceRecord> ReadAsync(
        ConnectorReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var reader = new StreamReader(_options.FilePath);

        string[]? header = null;
        if (_options.HasHeader)
        {
            var headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (headerLine is null)
            {
                yield break;
            }

            header = [.. CsvRowParser.ParseLine(headerLine, _options.Delimiter)];
        }

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var values = CsvRowParser.ParseLine(line, _options.Delimiter);
            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < values.Count; index++)
            {
                var name = header is not null && index < header.Length
                    ? header[index]
                    : $"col{index.ToString(CultureInfo.InvariantCulture)}";
                fields[name] = values[index];
            }

            yield return new SourceRecord(_options.SourceEntity, fields);
        }
    }
}
