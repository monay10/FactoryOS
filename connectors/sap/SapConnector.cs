using System.Data.Common;
using System.Runtime.CompilerServices;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Sap;

/// <summary>
/// A connector for SAP ERP. It reads the material master (<c>MARA</c>) with its description
/// (<c>MAKT</c>) and unrestricted stock summed across storage locations (<c>MARD.LABST</c>), emitting one
/// raw <see cref="SourceRecord"/> per material. SAP's dialect is normalized to the Standard Model by the
/// mapping.
/// </summary>
public sealed class SapConnector : IConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "sap";

    /// <summary>The source-entity name every emitted record is tagged with.</summary>
    public const string MaterialEntity = "MARA";

    private readonly Func<DbConnection> _connectionFactory;
    private readonly string _language;

    /// <summary>Initializes a new instance of the <see cref="SapConnector"/> class.</summary>
    /// <param name="connectionFactory">A factory that creates connections to the SAP database.</param>
    /// <param name="language">The SAP language key (<c>SPRAS</c>) used to select the material text; defaults to <c>E</c>.</param>
    public SapConnector(Func<DbConnection> connectionFactory, string language = "E")
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        _connectionFactory = connectionFactory;
        _language = language;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string SourceSystem => "SAP";

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
            "SELECT m.MATNR AS MATNR, t.MAKTX AS MAKTX, m.MEINS AS MEINS, " +
            "COALESCE(SUM(d.LABST), 0) AS LABST " +
            "FROM MARA m " +
            "LEFT JOIN MAKT t ON t.MATNR = m.MATNR AND t.SPRAS = @language " +
            "LEFT JOIN MARD d ON d.MATNR = m.MATNR " +
            "GROUP BY m.MATNR, t.MAKTX, m.MEINS " +
            "ORDER BY m.MATNR";

        var languageParameter = command.CreateParameter();
        languageParameter.ParameterName = "@language";
        languageParameter.Value = _language;
        command.Parameters.Add(languageParameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                fields[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            yield return new SourceRecord(MaterialEntity, fields);
        }
    }
}
