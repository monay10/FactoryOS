using FactoryOS.Connectors.Log;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.DeliveryHealth;
using FactoryOS.Plugins.DeliveryHealth.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The notification audit trail closed on the read side over the real bus, zero inter-module references: a
/// dispatched notification (<see cref="NotificationDispatched"/> on the <c>log</c> transport) is delivered by the
/// log outbound connector, which announces <see cref="NotificationDelivered"/>, and the Delivery Health module
/// folds that outcome into its per-transport tallies. Connector and read model compose only through shared
/// contracts. `NotificationDispatched → NotificationDelivered → delivery-health tally`.
/// </summary>
public sealed class DeliveryHealthChainTests
{
    [Fact]
    public async Task A_dispatched_notification_is_delivered_and_counted_as_healthy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        new LogConnectorPlugin().ConfigureServices(services);
        new DeliveryHealthPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var health = provider.GetRequiredService<IDeliveryHealthStore>();

        await bus.PublishAsync(new NotificationDispatched
        {
            Tenant = "acme",
            Channel = "ops",
            Transport = "log",
            Priority = "Normal",
            Subject = "Pump maintenance due",
            Action = "Notify",
            DispatchedAt = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero),
            SourceEventId = Guid.NewGuid(),
        });

        var tally = Assert.Single(health.ForTenant("acme"));
        Assert.Equal("log", tally.Transport);
        Assert.Equal(1, tally.Attempts);
        Assert.Equal(1, tally.Delivered);
        Assert.Equal(0, tally.Failed);
        Assert.Empty(health.RecentFailures("acme", 10));
    }
}
