using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard;
using FactoryOS.Plugins.Dashboard.Domain;
using FactoryOS.Plugins.DeliveryHealth;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// A delivery degradation reaching the Experience layer over the real bus, zero inter-module references: repeated
/// failed deliveries (<see cref="NotificationDelivered"/> with <c>Delivered=false</c>) push a transport's streak over
/// the Delivery Health threshold, which raises <see cref="DeliveryHealthDegraded"/>, and the Dashboard folds that
/// alert into the operations board as a warning tile. Delivery Health and Dashboard compose only through shared
/// contracts. `NotificationDelivered×N → DeliveryHealthDegraded → operations board`.
/// </summary>
public sealed class DeliveryToDashboardSpineTests
{
    private static NotificationDelivered Failed() => new()
    {
        EventId = Guid.NewGuid(),
        Tenant = "acme",
        Transport = "webhook",
        Channel = "ops",
        Subject = "Pump alert",
        Delivered = false,
        Detail = "503 Service Unavailable",
        DeliveredAt = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public async Task Repeated_failures_degrade_a_transport_and_land_on_the_board()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton(new DeliveryHealthOptions { FailureThreshold = 3 });

        new DeliveryHealthPlugin().ConfigureServices(services);
        new DashboardPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var board = provider.GetRequiredService<IOperationsBoard>();

        await bus.PublishAsync(Failed());
        await bus.PublishAsync(Failed());
        Assert.Empty(board.Snapshot("acme").RecentAlerts); // below threshold — no alert yet

        await bus.PublishAsync(Failed()); // third failure crosses the threshold

        var alert = Assert.Single(board.Snapshot("acme").RecentAlerts);
        Assert.Equal(nameof(DeliveryHealthDegraded), alert.Kind);
        Assert.Equal(AlertLevels.Warning, alert.Level);
        Assert.Contains("webhook", alert.Subject, StringComparison.Ordinal);
    }
}
