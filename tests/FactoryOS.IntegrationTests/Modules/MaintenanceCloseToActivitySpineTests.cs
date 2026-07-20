using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The close echoes across the bus with zero inter-module references: when Maintenance announces
/// <see cref="WorkOrderClosed"/>, the Activity Feed folds it into a per-tenant, newest-first "Maintenance" line
/// naming who closed it — without ever referencing the Maintenance module. Redelivery of the same close (same event
/// id) never doubles the entry. `WorkOrderClosed → activity feed`.
/// </summary>
public sealed class MaintenanceCloseToActivitySpineTests
{
    [Fact]
    public async Task A_closed_work_order_lands_on_the_activity_feed_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new ActivityPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var feed = provider.GetRequiredService<IActivityFeed>();

        var closed = new WorkOrderClosed
        {
            ClosedBy = "tech-1",
            WorkOrder = new WorkOrder
            {
                Tenant = "acme",
                Number = "WO-1",
                Title = "Inspect PUMP-1",
                Status = "Closed",
                AssetCode = "PUMP-1",
            },
        };

        await bus.PublishAsync(closed);
        await bus.PublishAsync(closed); // redelivery, same event id — must not double the entry

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Maintenance", entry.Category);
        Assert.Contains("WO-1", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("tech-1", entry.Headline, StringComparison.Ordinal);
    }
}
