using FactoryOS.Connectors.Manifest;

namespace FactoryOS.Tests.Connectors;

public sealed class ConnectorManifestReaderTests
{
    [Fact]
    public void Reads_a_valid_connector_manifest()
    {
        const string Json = """
        {
          "key": "logo",
          "name": "Logo Connector",
          "sourceSystem": "Logo",
          "provides": [ "InventoryItem" ],
          "mapping": "mapping.json"
        }
        """;

        var result = ConnectorManifestReader.Read(Json);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("logo", result.Value.Key);
        Assert.Equal("Logo", result.Value.SourceSystem);
        Assert.Equal("mapping.json", result.Value.Mapping);
        Assert.Contains("InventoryItem", result.Value.Provides);
    }

    [Fact]
    public void Rejects_a_manifest_without_a_source_system()
    {
        const string Json = """{ "key": "x", "name": "X" }""";

        var result = ConnectorManifestReader.Read(Json);

        Assert.True(result.IsFailure);
        Assert.Equal("Connector.Manifest.MissingSourceSystem", result.Error.Code);
    }
}
