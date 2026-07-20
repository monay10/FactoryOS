using System.Data.Common;
using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Logo;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;
using Microsoft.Data.Sqlite;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class LogoConnectorTests : IDisposable
{
    private const string ConnectionString = "Data Source=factoryos-logo-test;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public LogoConnectorTests()
    {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        using var command = _keepAlive.CreateCommand();
        command.CommandText =
            "CREATE TABLE LG_001_ITEMS (LOGICALREF INTEGER, CODE TEXT, NAME TEXT, UNIT TEXT);" +
            "CREATE TABLE LG_001_01_STINVTOT (STOCKREF INTEGER, ONHAND REAL);" +
            "INSERT INTO LG_001_ITEMS VALUES (1,'mlz-1','Steel','KG'),(2,'mlz-2','Copper','M'),(3,'mlz-3','NoStock','PCS');" +
            "INSERT INTO LG_001_01_STINVTOT VALUES (1, 1250.5), (2, 300);";
        command.ExecuteNonQuery();
    }

    private DbConnection CreateConnection() => new SqliteConnection(ConnectionString);

    [Theory]
    [InlineData(1, "LG_001_ITEMS")]
    [InlineData(25, "LG_025_ITEMS")]
    public void Builds_logo_table_names_by_convention(int firm, string expected)
    {
        Assert.Equal(expected, LogoObjectNames.Items(firm));
    }

    [Fact]
    public void Builds_period_scoped_table_names()
    {
        Assert.Equal("LG_001_01_STINVTOT", LogoObjectNames.StockTotals(1, 1));
    }

    [Fact]
    public async Task Reads_items_joined_with_stock_totals()
    {
        var connector = new LogoConnector(CreateConnection, new LogoConnectorOptions { FirmNumber = 1, PeriodNumber = 1 });

        var records = new List<SourceRecord>();
        await foreach (var record in connector.ReadAsync(new ConnectorReadContext("acme"), CancellationToken.None))
        {
            records.Add(record);
        }

        Assert.Equal(3, records.Count);
        Assert.Equal("ITEMS", records[0].SourceEntity);
        Assert.Equal("mlz-1", records[0].Fields["CODE"]);
        Assert.Equal(1250.5, records[0].Fields["ONHAND"]);
        Assert.Equal(0L, records[2].Fields["ONHAND"]); // no stock row → COALESCE 0
    }

    [Fact]
    public async Task Feeds_the_ingestion_pipeline_end_to_end()
    {
        var mapping = ConnectorAssets.Mapping("logo");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(
            new LogoConnector(CreateConnection, new LogoConnectorOptions { FirmNumber = 1, PeriodNumber = 1 }),
            mapping,
            new ConnectorReadContext("acme"),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Deduplicated);

        var binder = new StandardEntityBinder();
        var steel = Assert.IsType<InventoryItem>(binder.Bind(result.Records.Single(r => r.NaturalKey == "MLZ-1")).Value);
        Assert.Equal("Steel", steel.Name);
        Assert.Equal(1250.5m, steel.Quantity);
        Assert.Equal("kg", steel.Unit);

        var noStock = Assert.IsType<InventoryItem>(binder.Bind(result.Records.Single(r => r.NaturalKey == "MLZ-3")).Value);
        Assert.Equal(0m, noStock.Quantity);
    }

    public void Dispose() => _keepAlive.Dispose();
}
