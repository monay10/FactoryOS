using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Dashboard;
using FactoryOS.Plugins.Dashboard.Domain;
using FactoryOS.Plugins.Energy;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The energy-alert spine reaching the Experience layer over one bus, two plugins, zero inter-module references:
/// meter readings arrive (<see cref="MeterReadingReceived"/>), the Energy module folds them into a rolling baseline
/// and, once a reading runs far enough above that baseline, emits <see cref="EnergySpikeDetected"/> — which the
/// Dashboard folds into a warning alert on the live operations board without ever referencing the Energy module.
/// Redelivery of the spiking reading must not double the feed entry.
/// `MeterReadingReceived → EnergySpikeDetected → operations board`.
/// </summary>
public sealed class EnergyToDashboardSpineTests
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
    public async Task A_spike_lands_on_the_board_as_a_single_warning()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        new EnergyPlugin().ConfigureServices(services);
        new DashboardPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var board = provider.GetRequiredService<IOperationsBoard>();

        // Establish the baseline around 100 (defaults: MinimumSamples = 3).
        await bus.PublishAsync(Reading(100m));
        await bus.PublishAsync(Reading(100m));
        await bus.PublishAsync(Reading(100m));

        // A reading far above baseline (>25% default threshold) trips the spike.
        var spiking = Reading(200m);
        await bus.PublishAsync(spiking);
        await bus.PublishAsync(spiking); // redelivery, same event id — must not double the alert feed

        var alert = Assert.Single(board.Snapshot("acme").RecentAlerts);
        Assert.Equal(nameof(EnergySpikeDetected), alert.Kind);
        Assert.Equal(AlertLevels.Warning, alert.Level);
        Assert.Contains("main-incomer", alert.Subject, StringComparison.Ordinal);
    }
}
