using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Domain;
using FactoryOS.Plugins.Warehouse;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The low-stock spine over one bus, two plugins, zero inter-module references: a reorder point is defined
/// (<see cref="ItemReorderPointDefined"/>), stock movements apply to the ledger (<see cref="StockMovementRecorded"/>),
/// and when a movement first carries on-hand down to or below the reorder point the Warehouse module emits
/// <see cref="LowStockDetected"/> — which the Activity Feed folds into a per-tenant, newest-first "Warehouse" line
/// without ever referencing the Warehouse module. Redelivery of the crossing movement neither re-applies the ledger
/// nor doubles the feed entry. `StockMovementRecorded → LowStockDetected → activity feed`.
/// </summary>
public sealed class WarehouseToActivitySpineTests
{
    [Fact]
    public async Task A_low_stock_crossing_lands_on_the_activity_feed_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        new WarehousePlugin().ConfigureServices(services);
        new ActivityPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var feed = provider.GetRequiredService<IActivityFeed>();

        await bus.PublishAsync(new ItemReorderPointDefined
        {
            Tenant = "acme",
            WarehouseId = "wh-1",
            Sku = "SKU-1",
            ReorderPoint = 10m,
        });
        await bus.PublishAsync(new StockMovementRecorded
        {
            Tenant = "acme",
            WarehouseId = "wh-1",
            Sku = "SKU-1",
            QuantityDelta = 12m, // 0 → 12, still above the point
            OccurredAt = DateTimeOffset.UnixEpoch,
        });

        var crossing = new StockMovementRecorded
        {
            Tenant = "acme",
            WarehouseId = "wh-1",
            Sku = "SKU-1",
            QuantityDelta = -4m, // 12 → 8, crosses down to below 10
            OccurredAt = DateTimeOffset.UnixEpoch.AddHours(1),
        };
        await bus.PublishAsync(crossing);
        await bus.PublishAsync(crossing); // redelivery, same event id — must not double the entry

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Warehouse", entry.Category);
        Assert.Contains("SKU-1", entry.Headline, StringComparison.Ordinal);
    }
}
