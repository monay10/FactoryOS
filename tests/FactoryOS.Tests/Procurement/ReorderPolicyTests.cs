using FactoryOS.Plugins.Procurement;
using FactoryOS.Plugins.Procurement.Domain;

namespace FactoryOS.Tests.Procurement;

public sealed class ReorderPolicyTests
{
    private static readonly ProcurementOptions Options = new()
    {
        ReorderMultiple = 2m,
        MinimumOrderQuantity = 1m,
    };

    [Fact]
    public void Replenishes_up_to_the_reorder_multiple_of_the_point()
    {
        // target = 10 × 2 = 20; on-hand 8 → request 12.
        Assert.Equal(12m, ReorderPolicy.RequestedQuantity(onHand: 8m, reorderPoint: 10m, Options));
    }

    [Fact]
    public void Requests_at_least_the_minimum_order_quantity()
    {
        var options = new ProcurementOptions { ReorderMultiple = 1m, MinimumOrderQuantity = 5m };
        // target = 10 × 1 = 10; on-hand 10 → shortfall 0, floored to the minimum 5.
        Assert.Equal(5m, ReorderPolicy.RequestedQuantity(onHand: 10m, reorderPoint: 10m, options));
    }

    [Fact]
    public void A_deeper_shortfall_requests_more()
    {
        // target 20; on-hand 0 → request 20.
        Assert.Equal(20m, ReorderPolicy.RequestedQuantity(onHand: 0m, reorderPoint: 10m, Options));
    }
}
