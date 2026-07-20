using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Domain;
using FactoryOS.Plugins.Energy;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The energy-alert spine over one bus, two plugins, zero inter-module references: meter readings arrive
/// (<see cref="MeterReadingReceived"/>), the Energy module folds them into a rolling baseline and, once a reading
/// runs far enough above that baseline, emits <see cref="EnergySpikeDetected"/> — which the Activity Feed folds into
/// a per-tenant, newest-first "Energy" line without ever referencing the Energy module. Redelivery of the spiking
/// reading neither re-averages the baseline nor doubles the feed entry.
/// `MeterReadingReceived → EnergySpikeDetected → activity feed`.
/// </summary>
public sealed class EnergyToActivitySpineTests
{
    private static MeterReadingReceived Reading(decimal value, Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Reading = new MeterReading
        {
            Tenant = "acme",
            MeterId = "main-incomer",
            Metric = "ActivePower",
            Value = value,
            Unit = "kW",
            ReadingAt = DateTimeOffset.UnixEpoch,
        },
    };

    [Fact]
    public async Task A_spike_lands_on_the_activity_feed_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        new EnergyPlugin().ConfigureServices(services);
        new ActivityPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var feed = provider.GetRequiredService<IActivityFeed>();

        // Establish the baseline around 100 (defaults: MinimumSamples = 3).
        await bus.PublishAsync(Reading(100m));
        await bus.PublishAsync(Reading(100m));
        await bus.PublishAsync(Reading(100m));

        // A reading far above baseline (>25% default threshold) trips the spike.
        var spiking = Reading(200m);
        await bus.PublishAsync(spiking);
        await bus.PublishAsync(spiking); // redelivery, same event id — must not double the feed entry

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Energy", entry.Category);
        Assert.Contains("main-incomer", entry.Headline, StringComparison.Ordinal);
    }
}
