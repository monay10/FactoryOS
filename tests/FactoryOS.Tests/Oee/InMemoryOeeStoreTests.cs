using FactoryOS.Plugins.Oee.Domain;

namespace FactoryOS.Tests.Oee;

public sealed class InMemoryOeeStoreTests
{
    private static OeeSnapshot Snapshot(string tenant, string machine, DateTimeOffset periodEnd) =>
        new(tenant, machine, periodEnd.AddHours(-1), periodEnd, new OeeScore(0.9m, 0.9m, 0.9m, 0.729m));

    [Fact]
    public void The_same_machine_period_is_only_stored_once()
    {
        var store = new InMemoryOeeStore();
        var end = DateTimeOffset.UnixEpoch.AddHours(8);

        Assert.True(store.TryAdd(Snapshot("acme", "press-1", end)));
        Assert.False(store.TryAdd(Snapshot("acme", "press-1", end))); // duplicate machine-period
        Assert.Single(store.ForTenant("acme"));
    }

    [Fact]
    public void Different_periods_of_the_same_machine_are_kept()
    {
        var store = new InMemoryOeeStore();

        Assert.True(store.TryAdd(Snapshot("acme", "press-1", DateTimeOffset.UnixEpoch.AddHours(8))));
        Assert.True(store.TryAdd(Snapshot("acme", "press-1", DateTimeOffset.UnixEpoch.AddHours(16))));
        Assert.Equal(2, store.ForTenant("acme").Count);
    }

    [Fact]
    public void Tenants_are_isolated()
    {
        var store = new InMemoryOeeStore();
        var end = DateTimeOffset.UnixEpoch.AddHours(8);

        store.TryAdd(Snapshot("acme", "press-1", end));
        store.TryAdd(Snapshot("other", "press-1", end));

        Assert.Single(store.ForTenant("acme"));
        Assert.Single(store.ForTenant("other"));
        Assert.Empty(store.ForTenant("ghost"));
    }
}
