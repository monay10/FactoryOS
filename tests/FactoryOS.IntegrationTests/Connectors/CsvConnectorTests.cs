using FactoryOS.Connectors.Csv;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class CsvConnectorTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"factoryos-csv-{Guid.NewGuid():N}.csv");

    [Theory]
    [InlineData("a,b,c", new[] { "a", "b", "c" })]
    [InlineData("\"x,y\",z", new[] { "x,y", "z" })]
    [InlineData("\"he said \"\"hi\"\"\",end", new[] { "he said \"hi\"", "end" })]
    public void Parser_honours_quotes_and_escaped_quotes(string line, string[] expected)
    {
        Assert.Equal(expected, CsvRowParser.ParseLine(line, ','));
    }

    [Fact]
    public async Task Reads_rows_using_the_header_as_field_names()
    {
        await File.WriteAllTextAsync(_file, "sku,name,quantity\nA-1,Bolt,10\nA-2,Nut,20\n");

        var connector = new CsvConnector(new CsvConnectorOptions { FilePath = _file, SourceEntity = "Inventory" });

        var records = new List<SourceRecord>();
        await foreach (var record in connector.ReadAsync(new ConnectorReadContext("acme"), CancellationToken.None))
        {
            records.Add(record);
        }

        Assert.Equal(2, records.Count);
        Assert.Equal("Inventory", records[0].SourceEntity);
        Assert.Equal("A-1", records[0].Fields["sku"]);
        Assert.Equal("Bolt", records[0].Fields["name"]);
        Assert.Equal("10", records[0].Fields["quantity"]);
    }

    [Fact]
    public async Task Feeds_the_ingestion_pipeline_end_to_end()
    {
        await File.WriteAllTextAsync(_file, "sku,name,quantity,unit\na-1,  Bolt ,10.5,KG\n");

        var mapping = ConnectorAssets.Mapping("csv");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(
            new CsvConnector(new CsvConnectorOptions { FilePath = _file, SourceEntity = "Inventory" }),
            mapping,
            new ConnectorReadContext("acme"),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        var record = Assert.Single(result.Records);
        var item = Assert.IsType<InventoryItem>(new FactoryOS.Connectors.Binding.StandardEntityBinder().Bind(record).Value);
        Assert.Equal("A-1", item.Sku);
        Assert.Equal("Bolt", item.Name);
        Assert.Equal(10.5m, item.Quantity);
        Assert.Equal("kg", item.Unit);
    }

    public void Dispose()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }
    }
}
