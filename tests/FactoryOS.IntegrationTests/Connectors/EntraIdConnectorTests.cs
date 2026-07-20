using System.Net;
using System.Text;
using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.EntraId;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class EntraIdConnectorTests
{
    private sealed class GraphHandler : HttpMessageHandler
    {
        public string? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastAuthorization = request.Headers.Authorization?.ToString();
            var path = request.RequestUri!.AbsolutePath;
            var json = path.Contains("/users", StringComparison.OrdinalIgnoreCase) ? UsersJson : GroupsJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }

        private const string UsersJson = """
        { "value": [
            { "id": "1", "userPrincipalName": "Kerem@Contoso.onmicrosoft.com", "displayName": "Kerem Su", "mail": "Kerem@contoso.com", "accountEnabled": true },
            { "id": "2", "userPrincipalName": "gone@contoso.onmicrosoft.com", "displayName": "Gone Away", "mail": null, "accountEnabled": false }
        ] }
        """;

        private const string GroupsJson = """
        { "value": [
            { "id": "g1", "displayName": "Quality", "description": "QA reviewers" }
        ] }
        """;
    }

    private static (EntraIdConnector Connector, GraphHandler Handler) Build()
    {
        var handler = new GraphHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com") };
        return (new EntraIdConnector(client, new EntraIdConnectorOptions { AccessToken = "graph-token" }), handler);
    }

    [Fact]
    public async Task Reads_users_and_groups_with_a_bearer_token()
    {
        var (connector, handler) = Build();

        var records = new List<SourceRecord>();
        await foreach (var record in connector.ReadAsync(new ConnectorReadContext("acme"), CancellationToken.None))
        {
            records.Add(record);
        }

        Assert.Equal("Bearer graph-token", handler.LastAuthorization);
        Assert.Equal(2, records.Count(r => r.SourceEntity == EntraIdConnector.UsersEntity));
        Assert.Single(records, r => r.SourceEntity == EntraIdConnector.GroupsEntity);
    }

    [Fact]
    public async Task Feeds_the_ingestion_pipeline_into_standard_model_identity_entities()
    {
        var (connector, _) = Build();
        var mapping = ConnectorAssets.Mapping("entraid");
        var pipeline = new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

        var result = await pipeline.RunAsync(connector, mapping, new ConnectorReadContext("acme"), CancellationToken.None);

        Assert.Empty(result.Errors);

        var binder = new StandardEntityBinder();
        var kerem = Assert.IsType<DirectoryUser>(binder.Bind(result.Records.Single(r => r.NaturalKey == "kerem@contoso.onmicrosoft.com")).Value);
        Assert.Equal("Kerem Su", kerem.DisplayName);
        Assert.Equal("kerem@contoso.com", kerem.Email);
        Assert.True(kerem.Enabled);

        var gone = Assert.IsType<DirectoryUser>(binder.Bind(result.Records.Single(r => r.NaturalKey == "gone@contoso.onmicrosoft.com")).Value);
        Assert.False(gone.Enabled);
        Assert.Null(gone.Email);

        var group = Assert.IsType<DirectoryGroup>(binder.Bind(result.Records.Single(r => r.NaturalKey == "Quality")).Value);
        Assert.Equal("QA reviewers", group.Description);
    }
}
