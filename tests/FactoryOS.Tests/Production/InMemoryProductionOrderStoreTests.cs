using FactoryOS.Plugins.Production.Domain;

namespace FactoryOS.Tests.Production;

public sealed class InMemoryProductionOrderStoreTests
{
    private static ProductionOrderKey Key(string tenant = "acme", string order = "PO-1") => new(tenant, order);

    [Fact]
    public void Registration_is_idempotent()
    {
        var store = new InMemoryProductionOrderStore(allowOverProduction: true);

        Assert.True(store.TryRegister(Key(), "widget", 100));
        Assert.False(store.TryRegister(Key(), "widget", 100)); // already known
        Assert.Single(store.ForTenant("acme"));
    }

    [Fact]
    public void Accrual_against_an_unknown_order_is_not_found()
    {
        var store = new InMemoryProductionOrderStore(allowOverProduction: true);

        var result = store.Accrue(Key(), 10);

        Assert.False(result.Found);
        Assert.False(result.JustCompleted);
    }

    [Fact]
    public void Just_completed_fires_once_when_the_target_is_reached()
    {
        var store = new InMemoryProductionOrderStore(allowOverProduction: true);
        store.TryRegister(Key(), "widget", 100);

        Assert.False(store.Accrue(Key(), 60).JustCompleted); // 60 < 100
        var completing = store.Accrue(Key(), 40); // 100 >= 100
        Assert.True(completing.JustCompleted);
        Assert.Equal(100, completing.TotalProduced);

        Assert.False(store.Accrue(Key(), 10).JustCompleted); // already completed
    }

    [Fact]
    public void Over_production_accrues_past_the_target_when_allowed()
    {
        var store = new InMemoryProductionOrderStore(allowOverProduction: true);
        store.TryRegister(Key(), "widget", 100);

        var result = store.Accrue(Key(), 130);

        Assert.True(result.JustCompleted);
        Assert.Equal(130, result.TotalProduced); // not capped
    }

    [Fact]
    public void Over_production_is_capped_at_the_target_when_disallowed()
    {
        var store = new InMemoryProductionOrderStore(allowOverProduction: false);
        store.TryRegister(Key(), "widget", 100);

        var completing = store.Accrue(Key(), 130);
        Assert.True(completing.JustCompleted);
        Assert.Equal(100, completing.TotalProduced); // capped

        var ignored = store.Accrue(Key(), 50);
        Assert.False(ignored.JustCompleted);
        Assert.Equal(100, ignored.TotalProduced); // further counts ignored
    }

    [Fact]
    public void Orders_are_isolated_per_tenant()
    {
        var store = new InMemoryProductionOrderStore(allowOverProduction: true);
        store.TryRegister(Key("acme"), "widget", 100);
        store.TryRegister(Key("other"), "gadget", 50);

        Assert.Single(store.ForTenant("acme"));
        Assert.Single(store.ForTenant("other"));
        Assert.Empty(store.ForTenant("ghost"));
    }
}
