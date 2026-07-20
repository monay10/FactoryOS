using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Sample;
using FactoryOS.Connectors.Transforms;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.IntegrationTests.Connectors;

public sealed class IngestionPipelineTests
{
    private static MappingManifest SampleMapping() => ConnectorAssets.Mapping("sample");

    private static IIngestionPipeline BuildPipeline() =>
        new IngestionPipeline(new RecordNormalizer(new ValueTransformer()), new RecordDeduplicator());

    [Fact]
    public async Task Reads_normalizes_and_deduplicates_the_sample_logo_source()
    {
        var result = await BuildPipeline().RunAsync(
            new SampleLogoConnector(),
            SampleMapping(),
            new ConnectorReadContext("acme"),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Read);
        Assert.Equal(3, result.Normalized);
        Assert.Equal(2, result.Deduplicated);

        var steel = result.Records.Single(record => record.NaturalKey == "MLZ-001");
        Assert.Equal("Logo", steel.SourceSystem);
        Assert.Equal("InventoryItem", steel.EntityType);
        Assert.Equal("MLZ-001", steel.Values["Sku"]);          // upper-cased
        Assert.Equal("Steel Sheet 2mm", steel.Values["Name"]);  // trimmed
        Assert.Equal("kg", steel.Values["Unit"]);               // lower-cased
        Assert.Equal(1180.0m, steel.Values["Quantity"]);        // later read wins after dedup
    }

    [Fact]
    public async Task Normalized_records_bind_to_typed_standard_model_entities()
    {
        var result = await BuildPipeline().RunAsync(
            new SampleLogoConnector(),
            SampleMapping(),
            new ConnectorReadContext("acme"),
            CancellationToken.None);

        var binder = new StandardEntityBinder();
        var steel = result.Records.Single(record => record.NaturalKey == "MLZ-001");

        var bound = binder.Bind(steel);

        Assert.True(bound.IsSuccess, bound.IsFailure ? bound.Error.Description : null);
        var item = Assert.IsType<InventoryItem>(bound.Value);
        Assert.Equal("acme", item.Tenant);
        Assert.Equal("MLZ-001", item.Sku);
        Assert.Equal(1180.0m, item.Quantity);
        Assert.Equal("kg", item.Unit);
        Assert.Equal("A-01", item.Location);
    }
}
