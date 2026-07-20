using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Domain;
using FactoryOS.Plugins.Maintenance;
using FactoryOS.Plugins.RuleEngine;
using FactoryOS.Plugins.RuleEngine.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Company Brain proven over the real bus, and the bus's fan-out with it: a reading that crosses a rule
/// threshold produces one <see cref="RuleTriggered"/> that is consumed by <em>both</em> the Maintenance module
/// (which raises a work order) and the Company Brain (which records a feed entry) — plus the resulting
/// <see cref="WorkOrderCreated"/>, also recorded. Two independent subscribers, no shared reference, one event.
/// </summary>
public sealed class ActivityFeedTests
{
    [Fact]
    public async Task Noteworthy_events_land_in_the_activity_feed_via_bus_fan_out()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        services.AddSingleton(new RuleEngineOptions
        {
            Rules =
            [
                new RuleDefinition
                {
                    Id = "overtemp-press-1",
                    Metric = "Temperature",
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 85m,
                    Action = "RaiseMaintenanceAlert",
                },
            ],
        });

        new RuleEnginePlugin().ConfigureServices(services);
        new MaintenancePlugin().ConfigureServices(services);
        new ActivityPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var feed = provider.GetRequiredService<IActivityFeed>();

        await bus.PublishAsync(new MeterReadingReceived
        {
            Reading = new MeterReading
            {
                Tenant = "acme",
                MeterId = "press-1",
                Metric = "Temperature",
                Value = 90m,
                Unit = "°C",
                ReadingAt = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
            },
        });

        var recent = feed.Recent("acme", 10);
        Assert.Contains(recent, e => e.Category == "Rule");
        Assert.Contains(recent, e => e.Category == "Maintenance");
    }
}
