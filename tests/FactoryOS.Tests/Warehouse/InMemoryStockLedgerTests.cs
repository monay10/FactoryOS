using FactoryOS.Plugins.Warehouse.Domain;

namespace FactoryOS.Tests.Warehouse;

public sealed class InMemoryStockLedgerTests
{
    private static WarehouseStockKey Key(string tenant = "acme", string sku = "SKU-1") => new(tenant, "wh-1", sku);

    [Fact]
    public void Apply_accumulates_signed_deltas_and_reports_before_and_after()
    {
        var ledger = new InMemoryStockLedger();

        var first = ledger.Apply(Key(), 100m);
        Assert.Equal(0m, first.Previous);
        Assert.Equal(100m, first.Current);

        var second = ledger.Apply(Key(), -30m);
        Assert.Equal(100m, second.Previous);
        Assert.Equal(70m, second.Current);
    }

    [Fact]
    public void Reorder_point_is_stored_and_replaced_last_write_wins()
    {
        var ledger = new InMemoryStockLedger();

        Assert.Null(ledger.GetReorderPoint(Key()));
        ledger.SetReorderPoint(Key(), 10m);
        Assert.Equal(10m, ledger.GetReorderPoint(Key()));
        ledger.SetReorderPoint(Key(), 25m);
        Assert.Equal(25m, ledger.GetReorderPoint(Key()));
    }

    [Fact]
    public void Levels_are_isolated_per_tenant()
    {
        var ledger = new InMemoryStockLedger();

        ledger.Apply(Key("acme"), 100m);
        ledger.Apply(Key("other"), 5m);

        Assert.Equal(100m, Assert.Single(ledger.ForTenant("acme")).OnHand);
        Assert.Equal(5m, Assert.Single(ledger.ForTenant("other")).OnHand);
        Assert.Empty(ledger.ForTenant("ghost"));
    }
}
