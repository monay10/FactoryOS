using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Domain;
using FactoryOS.Plugins.Production;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The production-milestone spine over one bus, two plugins, zero inter-module references: an order is released
/// (<see cref="ProductionOrderReleased"/>), counts accrue against it (<see cref="ProductionCountReported"/>), and
/// when an increment first carries the order to target the Production module emits <see cref="ProductionOrderCompleted"/>
/// — which the Activity Feed folds into a per-tenant, newest-first "Production" line without ever referencing the
/// Production module. Redelivery of the completing count neither double-counts the order nor doubles the feed entry.
/// `ProductionOrderReleased + ProductionCountReported → ProductionOrderCompleted → activity feed`.
/// </summary>
public sealed class ProductionToActivitySpineTests
{
    [Fact]
    public async Task A_completed_order_lands_on_the_activity_feed_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        new ProductionPlugin().ConfigureServices(services);
        new ActivityPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var feed = provider.GetRequiredService<IActivityFeed>();

        await bus.PublishAsync(new ProductionOrderReleased
        {
            Tenant = "acme",
            OrderId = "PO-42",
            ProductId = "widget",
            TargetQuantity = 10,
            ReleasedAt = DateTimeOffset.UnixEpoch,
        });
        await bus.PublishAsync(new ProductionCountReported
        {
            Tenant = "acme",
            OrderId = "PO-42",
            ProducedCount = 6, // 0 → 6, below target
            ReportedAt = DateTimeOffset.UnixEpoch.AddHours(1),
        });

        var completingId = Guid.NewGuid();
        var completing = new ProductionCountReported
        {
            EventId = completingId,
            Tenant = "acme",
            OrderId = "PO-42",
            ProducedCount = 5, // 6 → 11, crosses the target of 10
            ReportedAt = DateTimeOffset.UnixEpoch.AddHours(2),
        };
        await bus.PublishAsync(completing);
        await bus.PublishAsync(completing); // redelivery, same event id — must not double the order or the entry

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Production", entry.Category);
        Assert.Contains("PO-42", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("11/10", entry.Headline, StringComparison.Ordinal);
    }
}
