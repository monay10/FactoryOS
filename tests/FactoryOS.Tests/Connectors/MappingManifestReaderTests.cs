using FactoryOS.Connectors.Manifest;

namespace FactoryOS.Tests.Connectors;

public sealed class MappingManifestReaderTests
{
    [Fact]
    public void Reads_a_valid_mapping_and_converts_scalar_constants_and_defaults()
    {
        const string Json = """
        {
          "sourceSystem": "Logo",
          "entities": [
            {
              "sourceEntity": "LG_STLINE",
              "targetEntity": "InventoryItem",
              "naturalKey": [ "Sku" ],
              "fields": [
                { "target": "Sku", "source": "STOKKODU", "transform": "upper", "required": true },
                { "target": "Quantity", "source": "MIKTAR", "transform": "decimal", "default": 0 },
                { "target": "Kind", "constant": "raw-material" }
              ]
            }
          ]
        }
        """;

        var result = MappingManifestReader.Read(Json);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var entity = Assert.Single(result.Value.Entities);
        Assert.Equal("LG_STLINE", entity.SourceEntity);
        Assert.Equal(["Sku"], entity.NaturalKey);

        var quantity = entity.Fields.Single(field => field.Target == "Quantity");
        Assert.Equal(0L, quantity.Default);

        var kind = entity.Fields.Single(field => field.Target == "Kind");
        Assert.Equal("raw-material", kind.Constant);
    }

    [Fact]
    public void Rejects_an_entity_without_a_natural_key()
    {
        const string Json = """
        {
          "sourceSystem": "Logo",
          "entities": [ { "sourceEntity": "A", "targetEntity": "InventoryItem", "fields": [] } ]
        }
        """;

        var result = MappingManifestReader.Read(Json);

        Assert.True(result.IsFailure);
        Assert.Equal("Connector.Mapping.MissingNaturalKey", result.Error.Code);
    }
}
