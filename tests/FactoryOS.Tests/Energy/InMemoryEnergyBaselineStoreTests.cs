using FactoryOS.Plugins.Energy.Domain;

namespace FactoryOS.Tests.Energy;

public sealed class InMemoryEnergyBaselineStoreTests
{
    private static EnergyMeterKey Key(string meter = "m1") => new("acme", meter, "ActivePower");

    [Fact]
    public void Observe_returns_the_baseline_before_folding_in_the_value()
    {
        var store = new InMemoryEnergyBaselineStore(windowSize: 10);

        var first = store.Observe(Key(), 100m);
        var second = store.Observe(Key(), 200m);
        var third = store.Observe(Key(), 300m);

        Assert.Equal(new BaselineSnapshot(0, 0m), first);
        Assert.Equal(new BaselineSnapshot(1, 100m), second);
        Assert.Equal(new BaselineSnapshot(2, 150m), third); // avg of 100, 200
    }

    [Fact]
    public void The_window_is_bounded_and_drops_the_oldest_value()
    {
        var store = new InMemoryEnergyBaselineStore(windowSize: 2);
        store.Observe(Key(), 100m);
        store.Observe(Key(), 200m);

        // Window now holds [100, 200]; adding 300 drops 100. Snapshot is the average of [100, 200].
        var snapshot = store.Observe(Key(), 300m);
        Assert.Equal(150m, snapshot.PriorAverage);

        // Next snapshot averages [200, 300] only — 100 has rolled off.
        var next = store.Observe(Key(), 400m);
        Assert.Equal(250m, next.PriorAverage);
    }

    [Fact]
    public void Aggregates_are_isolated_from_one_another()
    {
        var store = new InMemoryEnergyBaselineStore(windowSize: 10);
        store.Observe(Key("m1"), 100m);
        store.Observe(Key("m1"), 100m);

        var other = store.Observe(Key("m2"), 5m);

        Assert.Equal(new BaselineSnapshot(0, 0m), other); // m2 has its own fresh window
    }
}
