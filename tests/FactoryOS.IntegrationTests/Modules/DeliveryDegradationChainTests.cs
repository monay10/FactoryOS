using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Domain;
using FactoryOS.Plugins.DeliveryHealth;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// A delivery degradation surfaced on the timeline over the real bus, zero inter-module references: repeated failed
/// deliveries (<see cref="NotificationDelivered"/> with <c>Delivered=false</c>) push a transport's streak over the
/// Delivery Health threshold, which raises <see cref="DeliveryHealthDegraded"/>, and the Activity Feed folds that
/// alert into the factory timeline. Delivery Health and Activity compose only through shared contracts.
/// `NotificationDelivered×N → DeliveryHealthDegraded → activity entry`.
/// </summary>
public sealed class DeliveryDegradationChainTests
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
    public async Task Repeated_failures_degrade_a_transport_and_land_on_the_timeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton(new DeliveryHealthOptions { FailureThreshold = 3 });

        new DeliveryHealthPlugin().ConfigureServices(services);
        new ActivityPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var feed = provider.GetRequiredService<IActivityFeed>();

        await bus.PublishAsync(Failed());
        await bus.PublishAsync(Failed());
        Assert.Empty(feed.Recent("acme", 10)); // below threshold — no alert yet

        await bus.PublishAsync(Failed()); // third failure crosses the threshold

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Delivery", entry.Category);
        Assert.Contains("webhook", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("3 consecutive failures", entry.Headline, StringComparison.Ordinal);
    }
}
