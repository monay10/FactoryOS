using System.Data.Common;
using System.Runtime.CompilerServices;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Logo;

/// <summary>
/// A connector for the Logo ERP (Tiger/GO). It reads the firm's item master joined to the period's stock
/// totals, yielding one raw <see cref="SourceRecord"/> per item tagged <c>ITEMS</c>. Logo's dialect
/// (<c>CODE</c>, <c>NAME</c>, <c>ONHAND</c>, …) is normalized to the Standard Model by the mapping — the
/// connector itself never speaks Standard Model.
/// </summary>
public sealed class LogoConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "logo";

    /// <summary>The source-entity name every emitted record is tagged with.</summary>
    public const string ItemsEntity = "ITEMS";

    private readonly Func<DbConnection> _connectionFactory;
    private readonly LogoConnectorOptions _options;

    /// <summary>Initializes a new instance of the <see cref="LogoConnector"/> class.</summary>
    /// <param name="connectionFactory">A factory that creates connections to the Logo database.</param>
    /// <param name="options">The firm/period configuration.</param>
    public LogoConnector(Func<DbConnection> connectionFactory, LogoConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(options);
        _connectionFactory = connectionFactory;
        _options = options;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "Logo";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceRecord> ReadAsync(
        ConnectorReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var items = LogoObjectNames.Items(_options.FirmNumber);
        var totals = LogoObjectNames.StockTotals(_options.FirmNumber, _options.PeriodNumber);

        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();

        // Table names are built from validated firm/period integers, not from user input, so string
        // interpolation here cannot introduce SQL injection.
        command.CommandText =
            $"SELECT i.CODE AS CODE, i.NAME AS NAME, i.UNIT AS UNIT, COALESCE(s.ONHAND, 0) AS ONHAND " +
            $"FROM {items} i LEFT JOIN {totals} s ON s.STOCKREF = i.LOGICALREF " +
            $"ORDER BY i.CODE";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                fields[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            yield return new SourceRecord(ItemsEntity, fields);
        }
    }
}
