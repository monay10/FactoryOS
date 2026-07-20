using FactoryOS.Plugins.Warehouse.Domain;

namespace FactoryOS.Tests.Warehouse;

public sealed class LowStockEvaluatorTests
{
    [Fact]
    public void Fires_on_the_downward_crossing()
    {
        // 12 → 8, reorder point 10: crosses down.
        Assert.True(LowStockEvaluator.CrossedDown(new StockChange(12m, 8m), 10m));
    }

    [Fact]
    public void Fires_when_landing_exactly_on_the_point()
    {
        Assert.True(LowStockEvaluator.CrossedDown(new StockChange(12m, 10m), 10m));
    }

    [Fact]
    public void Does_not_refire_while_already_below()
    {
        // 8 → 6, both below the point of 10: no new crossing.
        Assert.False(LowStockEvaluator.CrossedDown(new StockChange(8m, 6m), 10m));
    }

    [Fact]
    public void Does_not_fire_on_a_receipt_that_stays_above()
    {
        Assert.False(LowStockEvaluator.CrossedDown(new StockChange(20m, 25m), 10m));
    }

    [Fact]
    public void A_non_positive_reorder_point_disables_detection()
    {
        Assert.False(LowStockEvaluator.CrossedDown(new StockChange(5m, -3m), 0m));
    }
}
