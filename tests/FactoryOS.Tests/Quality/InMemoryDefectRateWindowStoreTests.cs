using FactoryOS.Plugins.Quality.Domain;

namespace FactoryOS.Tests.Quality;

public sealed class InMemoryDefectRateWindowStoreTests
{
    private static QualityLineKey Key(string tenant = "acme") => new(tenant, "line-1", "widget");

    [Fact]
    public void Fold_accumulates_units_and_defects_across_inspections()
    {
        var store = new InMemoryDefectRateWindowStore(windowSize: 10);

        store.Fold(Key(), 50, 1);
        var window = store.Fold(Key(), 50, 4);

        Assert.Equal(100, window.InspectedUnits);
        Assert.Equal(5, window.DefectiveUnits);
        Assert.Equal(0.05m, window.DefectRate);
    }

    [Fact]
    public void The_window_drops_the_oldest_inspection_once_full()
    {
        var store = new InMemoryDefectRateWindowStore(windowSize: 2);

        store.Fold(Key(), 10, 9); // will be evicted
        store.Fold(Key(), 10, 1);
        var window = store.Fold(Key(), 10, 1); // evicts the first sample

        Assert.Equal(20, window.InspectedUnits);
        Assert.Equal(2, window.DefectiveUnits);
    }

    [Fact]
    public void Aggregates_are_isolated_per_tenant_and_key()
    {
        var store = new InMemoryDefectRateWindowStore(windowSize: 10);

        store.Fold(Key("acme"), 100, 10);
        var other = store.Fold(Key("other"), 100, 1);

        Assert.Equal(100, other.InspectedUnits);
        Assert.Equal(1, other.DefectiveUnits); // untouched by acme's window
    }
}
