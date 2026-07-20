using System.Data.Common;
using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Sql;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;
using Microsoft.Data.Sqlite;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class SqlConnectorTests : IDisposable
{
    private const string ConnectionString = "Data Source=factoryos-sql-test;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public SqlConnectorTests()
    {
        // A shared in-memory database lives only while at least one connection is open; this one keeps it
        // alive for the test while the connector opens its own connections through the factory.
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        using var command = _keepAlive.CreateCommand();
        command.CommandText =
            "CREATE TABLE stock (sku TEXT, name TEXT, quantity REAL, unit TEXT);" +
            "INSERT INTO stock VALUES ('a-1', 'Bolt', 10.5, 'KG'), ('a-2', 'Nut', 5, 'PCS');";
        command.ExecuteNonQuery();
    }

    private sealed class SharedSqliteConnectionFactory : IDbConnectionFactory
    {
        public DbConnection Create() => new SqliteConnection(ConnectionString);
    }

    [Fact]
    public async Task Reads_each_result_row_as_a_source_record()
    {
        var connector = new SqlConnector(
            new SharedSqliteConnectionFactory(),
            new SqlConnectorOptions { Query = "SELECT sku, name, quantity, unit FROM stock ORDER BY sku", SourceEntity = "stock" });

        var records = new List<SourceRecord>();
        await foreach (var record in connector.ReadAsync(new ConnectorReadContext("acme"), CancellationToken.None))
        {
            records.Add(record);
        }

        Assert.Equal(2, records.Count);
        Assert.Equal("stock", records[0].SourceEntity);
        Assert.Equal("a-1", records[0].Fields["sku"]);
        Assert.Equal("Bolt", records[0].Fields["name"]);
    }

    [Fact]
    public async Task Feeds_the_ingestion_pipeline_end_to_end()
    {
        var mapping = ConnectorAssets.Mapping("sql");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(
            new SqlConnector(
                new SharedSqliteConnectionFactory(),
                new SqlConnectorOptions { Query = "SELECT sku, name, quantity, unit FROM stock ORDER BY sku", SourceEntity = "stock" }),
            mapping,
            new ConnectorReadContext("acme"),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Deduplicated);

        var binder = new StandardEntityBinder();
        var bolt = Assert.IsType<InventoryItem>(binder.Bind(result.Records.Single(r => r.NaturalKey == "A-1")).Value);
        Assert.Equal("Bolt", bolt.Name);
        Assert.Equal(10.5m, bolt.Quantity);
        Assert.Equal("kg", bolt.Unit);
    }

    public void Dispose() => _keepAlive.Dispose();
}
