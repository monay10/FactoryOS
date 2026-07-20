using FactoryOS.Connectors.Deduplication;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Tests.Connectors;

public sealed class RecordDeduplicatorTests
{
    private readonly IRecordDeduplicator _deduplicator = new RecordDeduplicator();

    private static NormalizedRecord Record(string key, string tenant = "acme", string entity = "InventoryItem", decimal quantity = 0m) =>
        new(tenant, "Logo", entity, key, new Dictionary<string, object?> { ["Quantity"] = quantity });

    [Fact]
    public void Keeps_the_last_value_for_a_repeated_key()
    {
        var result = _deduplicator.Deduplicate(
        [
            Record("MLZ-1", quantity: 100m),
            Record("MLZ-2", quantity: 50m),
            Record("MLZ-1", quantity: 120m),
        ]);

        Assert.Equal(2, result.Count);
        Assert.Equal(120m, result[0].Values["Quantity"]);
        Assert.Equal("MLZ-1", result[0].NaturalKey);
        Assert.Equal("MLZ-2", result[1].NaturalKey);
    }

    [Fact]
    public void Treats_different_tenants_and_entity_types_as_distinct()
    {
        var result = _deduplicator.Deduplicate(
        [
            Record("K", tenant: "acme"),
            Record("K", tenant: "globex"),
            Record("K", entity: "Asset"),
        ]);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Returns_empty_for_an_empty_sequence()
    {
        Assert.Empty(_deduplicator.Deduplicate([]));
    }
}
