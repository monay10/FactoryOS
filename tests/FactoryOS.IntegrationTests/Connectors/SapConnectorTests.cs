using System.Data.Common;
using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Sap;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;
using Microsoft.Data.Sqlite;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class SapConnectorTests : IDisposable
{
    private const string ConnectionString = "Data Source=factoryos-sap-test;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public SapConnectorTests()
    {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        using var command = _keepAlive.CreateCommand();
        command.CommandText =
            "CREATE TABLE MARA (MATNR TEXT, MEINS TEXT);" +
            "CREATE TABLE MAKT (MATNR TEXT, SPRAS TEXT, MAKTX TEXT);" +
            "CREATE TABLE MARD (MATNR TEXT, LABST REAL);" +
            "INSERT INTO MARA VALUES ('100-1','KG'),('100-2','ST');" +
            "INSERT INTO MAKT VALUES ('100-1','E','Steel Plate'),('100-1','D','Stahlplatte'),('100-2','E','Bolt');" +
            "INSERT INTO MARD VALUES ('100-1',60),('100-1',40),('100-2',10);";
        command.ExecuteNonQuery();
    }

    private DbConnection CreateConnection() => new SqliteConnection(ConnectionString);

    [Fact]
    public async Task Reads_material_with_english_text_and_summed_stock()
    {
        var mapping = ConnectorAssets.Mapping("sap");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(
            new SapConnector(CreateConnection),
            mapping,
            new ConnectorReadContext("acme"),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Deduplicated);

        var steel = Assert.IsType<InventoryItem>(
            new StandardEntityBinder().Bind(result.Records.Single(r => r.NaturalKey == "100-1")).Value);
        Assert.Equal("Steel Plate", steel.Name); // SPRAS = 'E' text
        Assert.Equal(100m, steel.Quantity);      // 60 + 40 across storage locations
        Assert.Equal("kg", steel.Unit);
    }

    public void Dispose() => _keepAlive.Dispose();
}
