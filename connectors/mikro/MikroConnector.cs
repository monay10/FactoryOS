using System.Data.Common;
using System.Runtime.CompilerServices;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Mikro;

/// <summary>
/// A connector for the Mikro ERP. It reads the stock master (<c>STOKLAR</c>) with the on-hand balance
/// aggregated from stock movements (<c>STOK_HAREKETLERI</c>, signed by movement type), emitting one raw
/// <see cref="SourceRecord"/> per item. Mikro's dialect is normalized to the Standard Model by the
/// mapping.
/// </summary>
public sealed class MikroConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "mikro";

    /// <summary>The source-entity name every emitted record is tagged with.</summary>
    public const string ItemsEntity = "STOKLAR";

    private readonly Func<DbConnection> _connectionFactory;

    /// <summary>Initializes a new instance of the <see cref="MikroConnector"/> class.</summary>
    /// <param name="connectionFactory">A factory that creates connections to the Mikro company database.</param>
    public MikroConnector(Func<DbConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "Mikro";

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
            "SELECT s.sto_kod AS sto_kod, s.sto_isim AS sto_isim, s.sto_birim1_ad AS sto_birim1_ad, " +
            "COALESCE(SUM(CASE WHEN h.sth_tip = 0 THEN h.sth_miktar ELSE -h.sth_miktar END), 0) AS bakiye " +
            "FROM STOKLAR s LEFT JOIN STOK_HAREKETLERI h ON h.sth_stok_kod = s.sto_kod " +
            "GROUP BY s.sto_kod, s.sto_isim, s.sto_birim1_ad " +
            "ORDER BY s.sto_kod";

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
