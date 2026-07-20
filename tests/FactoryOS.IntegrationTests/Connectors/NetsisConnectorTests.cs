using System.Data.Common;
using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Netsis;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;
using Microsoft.Data.Sqlite;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class NetsisConnectorTests : IDisposable
{
    private const string ConnectionString = "Data Source=factoryos-netsis-test;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public NetsisConnectorTests()
    {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        using var command = _keepAlive.CreateCommand();
        command.CommandText =
            "CREATE TABLE TBLSTSABIT (STOK_KODU TEXT, STOK_ADI TEXT, OLCU_BR1 TEXT);" +
            "CREATE TABLE TBLSTHAR (STOK_KODU TEXT, STHAR_GCKOD TEXT, STHAR_GCMIK REAL);" +
            "INSERT INTO TBLSTSABIT VALUES ('mlz-1','Steel','KG'),('mlz-2','Copper','M');" +
            "INSERT INTO TBLSTHAR VALUES ('mlz-1','G',100),('mlz-1','C',30),('mlz-2','G',50);";
        command.ExecuteNonQuery();
    }

    private DbConnection CreateConnection() => new SqliteConnection(ConnectionString);

    [Fact]
    public async Task Aggregates_stock_movements_into_an_on_hand_balance()
    {
        var mapping = ConnectorAssets.Mapping("netsis");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(
            new NetsisConnector(CreateConnection),
            mapping,
            new ConnectorReadContext("acme"),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Deduplicated);

        var binder = new StandardEntityBinder();
        var steel = Assert.IsType<InventoryItem>(binder.Bind(result.Records.Single(r => r.NaturalKey == "MLZ-1")).Value);
        Assert.Equal("Steel", steel.Name);
        Assert.Equal(70m, steel.Quantity); // 100 in − 30 out
        Assert.Equal("kg", steel.Unit);
    }

    public void Dispose() => _keepAlive.Dispose();
}
