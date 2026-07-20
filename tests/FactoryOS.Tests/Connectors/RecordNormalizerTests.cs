using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Tests.Connectors;

public sealed class RecordNormalizerTests
{
    private readonly IRecordNormalizer _normalizer = new RecordNormalizer(new ValueTransformer());

    private static MappingManifest LogoMapping(params FieldMapping[] fields) => new()
    {
        SourceSystem = "Logo",
        Entities =
        [
            new EntityMapping
            {
                SourceEntity = "LG_STLINE",
                TargetEntity = "InventoryItem",
                NaturalKey = ["Sku"],
                Fields = fields,
            },
        ],
    };

    private static SourceRecord StockRow(params (string Key, object? Value)[] fields) =>
        new("LG_STLINE", fields.ToDictionary(field => field.Key, field => field.Value, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void Maps_fields_applies_transforms_and_builds_the_natural_key()
    {
        var mapping = LogoMapping(
            new FieldMapping { Target = "Sku", Source = "STOKKODU", Transform = "upper", Required = true },
            new FieldMapping { Target = "Name", Source = "STOKADI", Transform = "trim" },
            new FieldMapping { Target = "Quantity", Source = "MIKTAR", Transform = "decimal" });

        var record = StockRow(("STOKKODU", "mlz-1"), ("STOKADI", "  Steel  "), ("MIKTAR", "12.5"));

        var result = _normalizer.Normalize(record, mapping, "acme");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("acme", result.Value.Tenant);
        Assert.Equal("Logo", result.Value.SourceSystem);
        Assert.Equal("InventoryItem", result.Value.EntityType);
        Assert.Equal("MLZ-1", result.Value.NaturalKey);
        Assert.Equal("MLZ-1", result.Value.Values["Sku"]);
        Assert.Equal("Steel", result.Value.Values["Name"]);
        Assert.Equal(12.5m, result.Value.Values["Quantity"]);
    }

    [Fact]
    public void Applies_constant_and_default_values()
    {
        var mapping = LogoMapping(
            new FieldMapping { Target = "Sku", Constant = "FIXED" },
            new FieldMapping { Target = "Unit", Source = "BIRIM", Default = "pcs" });

        var result = _normalizer.Normalize(StockRow(("BIRIM", null)), mapping, "acme");

        Assert.True(result.IsSuccess);
        Assert.Equal("FIXED", result.Value.Values["Sku"]);
        Assert.Equal("pcs", result.Value.Values["Unit"]);
    }

    [Fact]
    public void Fails_when_a_required_field_is_missing()
    {
        var mapping = LogoMapping(
            new FieldMapping { Target = "Sku", Source = "STOKKODU", Required = true });

        var result = _normalizer.Normalize(StockRow(("STOKKODU", null)), mapping, "acme");

        Assert.True(result.IsFailure);
        Assert.Equal("Connector.Normalize.RequiredFieldMissing", result.Error.Code);
    }

    [Fact]
    public void Fails_when_no_mapping_matches_the_source_entity()
    {
        var mapping = LogoMapping(new FieldMapping { Target = "Sku", Source = "STOKKODU" });

        var result = _normalizer.Normalize(new SourceRecord("UNKNOWN", new Dictionary<string, object?>()), mapping, "acme");

        Assert.True(result.IsFailure);
        Assert.Equal("Connector.Normalize.NoMapping", result.Error.Code);
    }

    [Fact]
    public void Propagates_a_transform_failure()
    {
        var mapping = LogoMapping(
            new FieldMapping { Target = "Sku", Source = "STOKKODU", Required = true },
            new FieldMapping { Target = "Quantity", Source = "MIKTAR", Transform = "decimal" });

        var result = _normalizer.Normalize(StockRow(("STOKKODU", "A"), ("MIKTAR", "oops")), mapping, "acme");

        Assert.True(result.IsFailure);
        Assert.Equal("Connector.Transform.Failed", result.Error.Code);
    }

    [Fact]
    public void Fails_when_the_natural_key_field_has_no_value()
    {
        var mapping = LogoMapping(new FieldMapping { Target = "Name", Source = "STOKADI" });

        var result = _normalizer.Normalize(StockRow(("STOKADI", "x")), mapping, "acme");

        Assert.True(result.IsFailure);
        Assert.Equal("Connector.Normalize.MissingNaturalKey", result.Error.Code);
    }
}
