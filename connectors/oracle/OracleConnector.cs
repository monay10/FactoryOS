using System.Data.Common;
using System.Runtime.CompilerServices;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Oracle;

/// <summary>
/// A connector for Oracle E-Business Suite inventory. It reads the item master
/// (<c>MTL_SYSTEM_ITEMS_B</c>) with on-hand summed from <c>MTL_ONHAND_QUANTITIES</c>, emitting one raw
/// <see cref="SourceRecord"/> per item. Oracle's dialect is normalized to the Standard Model by the
/// mapping.
/// </summary>
public sealed class OracleConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "oracle";

    /// <summary>The source-entity name every emitted record is tagged with.</summary>
    public const string ItemEntity = "MTL_SYSTEM_ITEMS";

    private readonly Func<DbConnection> _connectionFactory;

    /// <summary>Initializes a new instance of the <see cref="OracleConnector"/> class.</summary>
    /// <param name="connectionFactory">A factory that creates connections to the Oracle database.</param>
    public OracleConnector(Func<DbConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "Oracle";

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
            "SELECT i.SEGMENT1 AS SEGMENT1, i.DESCRIPTION AS DESCRIPTION, i.PRIMARY_UOM_CODE AS PRIMARY_UOM_CODE, " +
            "COALESCE(SUM(q.TRANSACTION_QUANTITY), 0) AS ONHAND " +
            "FROM MTL_SYSTEM_ITEMS_B i " +
            "LEFT JOIN MTL_ONHAND_QUANTITIES q ON q.INVENTORY_ITEM_ID = i.INVENTORY_ITEM_ID " +
            "GROUP BY i.SEGMENT1, i.DESCRIPTION, i.PRIMARY_UOM_CODE " +
            "ORDER BY i.SEGMENT1";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                fields[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            yield return new SourceRecord(ItemEntity, fields);
        }
    }
}
