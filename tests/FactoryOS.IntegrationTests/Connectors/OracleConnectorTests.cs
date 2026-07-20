using System.Data.Common;
using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Oracle;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;
using Microsoft.Data.Sqlite;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class OracleConnectorTests : IDisposable
{
    private const string ConnectionString = "Data Source=factoryos-oracle-test;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public OracleConnectorTests()
    {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        using var command = _keepAlive.CreateCommand();
        command.CommandText =
            "CREATE TABLE MTL_SYSTEM_ITEMS_B (INVENTORY_ITEM_ID INTEGER, SEGMENT1 TEXT, DESCRIPTION TEXT, PRIMARY_UOM_CODE TEXT);" +
            "CREATE TABLE MTL_ONHAND_QUANTITIES (INVENTORY_ITEM_ID INTEGER, TRANSACTION_QUANTITY REAL);" +
            "INSERT INTO MTL_SYSTEM_ITEMS_B VALUES (1,'as54888','Sentinel Desktop','Ea');" +
            "INSERT INTO MTL_ONHAND_QUANTITIES VALUES (1,120),(1,30);";
        command.ExecuteNonQuery();
    }

    private DbConnection CreateConnection() => new SqliteConnection(ConnectionString);

    [Fact]
    public async Task Reads_item_master_with_summed_on_hand()
    {
        var mapping = ConnectorAssets.Mapping("oracle");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(
            new OracleConnector(CreateConnection),
            mapping,
            new ConnectorReadContext("acme"),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        var item = Assert.IsType<InventoryItem>(
            new StandardEntityBinder().Bind(Assert.Single(result.Records)).Value);
        Assert.Equal("AS54888", item.Sku);
        Assert.Equal("Sentinel Desktop", item.Name);
        Assert.Equal(150m, item.Quantity); // 120 + 30
        Assert.Equal("ea", item.Unit);
    }

    public void Dispose() => _keepAlive.Dispose();
}
