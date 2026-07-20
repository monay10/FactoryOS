using System.Data.Common;
using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Mikro;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;
using Microsoft.Data.Sqlite;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class MikroConnectorTests : IDisposable
{
    private const string ConnectionString = "Data Source=factoryos-mikro-test;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public MikroConnectorTests()
    {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        using var command = _keepAlive.CreateCommand();
        command.CommandText =
            "CREATE TABLE STOKLAR (sto_kod TEXT, sto_isim TEXT, sto_birim1_ad TEXT);" +
            "CREATE TABLE STOK_HAREKETLERI (sth_stok_kod TEXT, sth_tip INTEGER, sth_miktar REAL);" +
            "INSERT INTO STOKLAR VALUES ('mk-1','Widget','ADET');" +
            "INSERT INTO STOK_HAREKETLERI VALUES ('mk-1',0,200),('mk-1',1,80);";
        command.ExecuteNonQuery();
    }

    private DbConnection CreateConnection() => new SqliteConnection(ConnectionString);

    [Fact]
    public async Task Aggregates_movements_by_type_into_an_on_hand_balance()
    {
        var mapping = ConnectorAssets.Mapping("mikro");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(
            new MikroConnector(CreateConnection),
            mapping,
            new ConnectorReadContext("acme"),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        var widget = Assert.IsType<InventoryItem>(
            new StandardEntityBinder().Bind(Assert.Single(result.Records)).Value);
        Assert.Equal("MK-1", widget.Sku);
        Assert.Equal("Widget", widget.Name);
        Assert.Equal(120m, widget.Quantity); // 200 in − 80 out
        Assert.Equal("adet", widget.Unit);
    }

    public void Dispose() => _keepAlive.Dispose();
}
