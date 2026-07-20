using System.Net;
using System.Text;
using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Rest;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class RestConnectorTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;

        public StubHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            });
    }

    private static HttpClient Client(string json) =>
        new(new StubHandler(json)) { BaseAddress = new Uri("https://erp.example.com/api/") };

    [Fact]
    public async Task Reads_objects_from_a_nested_array_path()
    {
        const string Json = """
        { "data": { "items": [
            { "code": "a-1", "title": " Bolt ", "stock": 10, "unit": "KG" },
            { "code": "a-2", "title": "Nut", "stock": 5, "unit": "PCS" }
        ] } }
        """;

        var connector = new RestConnector(
            Client(Json),
            new RestConnectorOptions { RequestPath = "products", SourceEntity = "Product", ArrayPath = "data.items" });

        var records = new List<SourceRecord>();
        await foreach (var record in connector.ReadAsync(new ConnectorReadContext("acme"), CancellationToken.None))
        {
            records.Add(record);
        }

        Assert.Equal(2, records.Count);
        Assert.Equal("a-1", records[0].Fields["code"]);
        Assert.Equal(10L, records[0].Fields["stock"]);
    }

    [Fact]
    public async Task Reads_a_root_level_array_when_no_path_is_set()
    {
        const string Json = """[ { "code": "a-1", "title": "Bolt", "stock": 1, "unit": "kg" } ]""";

        var connector = new RestConnector(
            Client(Json),
            new RestConnectorOptions { RequestPath = "products", SourceEntity = "Product" });

        var records = new List<SourceRecord>();
        await foreach (var record in connector.ReadAsync(new ConnectorReadContext("acme"), CancellationToken.None))
        {
            records.Add(record);
        }

        Assert.Single(records);
    }

    [Fact]
    public async Task Feeds_the_ingestion_pipeline_end_to_end()
    {
        const string Json = """
        { "data": { "items": [ { "code": "a-1", "title": " Bolt ", "stock": 10, "unit": "KG" } ] } }
        """;

        var mapping = ConnectorAssets.Mapping("rest");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(
            new RestConnector(
                Client(Json),
                new RestConnectorOptions { RequestPath = "products", SourceEntity = "Product", ArrayPath = "data.items" }),
            mapping,
            new ConnectorReadContext("acme"),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        var record = Assert.Single(result.Records);
        var item = Assert.IsType<InventoryItem>(new StandardEntityBinder().Bind(record).Value);
        Assert.Equal("A-1", item.Sku);
        Assert.Equal("Bolt", item.Name);
        Assert.Equal(10m, item.Quantity);
        Assert.Equal("kg", item.Unit);
    }
}
