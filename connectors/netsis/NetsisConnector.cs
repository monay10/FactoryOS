using System.Data.Common;
using System.Runtime.CompilerServices;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Netsis;

/// <summary>
/// A connector for the Netsis ERP. It reads the item master (<c>TBLSTSABIT</c>) with the on-hand balance
/// aggregated from stock movements (<c>TBLSTHAR</c>, signed by entry/exit), emitting one raw
/// <see cref="SourceRecord"/> per item. Netsis's dialect is normalized to the Standard Model by the
/// mapping.
/// </summary>
public sealed class NetsisConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "netsis";

    /// <summary>The source-entity name every emitted record is tagged with.</summary>
    public const string ItemsEntity = "TBLSTSABIT";

    private readonly Func<DbConnection> _connectionFactory;

    /// <summary>Initializes a new instance of the <see cref="NetsisConnector"/> class.</summary>
    /// <param name="connectionFactory">A factory that creates connections to the Netsis company database.</param>
    public NetsisConnector(Func<DbConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "Netsis";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceRecord> ReadAsync(
        ConnectorReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT s.STOK_KODU AS STOK_KODU, s.STOK_ADI AS STOK_ADI, s.OLCU_BR1 AS OLCU_BR1, " +
            "COALESCE(SUM(CASE WHEN h.STHAR_GCKOD = 'G' THEN h.STHAR_GCMIK ELSE -h.STHAR_GCMIK END), 0) AS BAKIYE " +
            "FROM TBLSTSABIT s LEFT JOIN TBLSTHAR h ON h.STOK_KODU = s.STOK_KODU " +
            "GROUP BY s.STOK_KODU, s.STOK_ADI, s.OLCU_BR1 " +
            "ORDER BY s.STOK_KODU";

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
