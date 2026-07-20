using System.Runtime.CompilerServices;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Sql;

/// <summary>
/// A generic connector that runs a SQL query and yields one <see cref="SourceRecord"/> per result row,
/// with the source column names as field keys. It is provider-agnostic: any ADO.NET provider works
/// through the injected connection factory.
/// </summary>
public sealed class SqlConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "sql";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly SqlConnectorOptions _options;

    /// <summary>Initializes a new instance of the <see cref="SqlConnector"/> class.</summary>
    /// <param name="connectionFactory">The factory that creates provider-specific connections.</param>
    /// <param name="options">The query configuration.</param>
    public SqlConnector(IDbConnectionFactory connectionFactory, SqlConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Query);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SourceEntity);
        _connectionFactory = connectionFactory;
        _options = options;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "SQL";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceRecord> ReadAsync(
        ConnectorReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = _options.Query;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                fields[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            yield return new SourceRecord(_options.SourceEntity, fields);
        }
    }
}
