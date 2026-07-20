using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Energy;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// Proves the Energy module works purely event-driven through the real in-process event bus: a
/// <see cref="MeterReadingReceived"/> published on the bus is consumed by the plugin's handler, which
/// publishes its own energy events back — no module referencing another, only the bus.
/// </summary>
public sealed class EnergyModuleTests
{
    private sealed class CaptureSink
    {
        public ConcurrentBag<IIntegrationEvent> Events { get; } = [];
    }

    private sealed class CapturingHandler<TEvent> : IEventHandler<TEvent>
        where TEvent : IIntegrationEvent
    {
        private readonly CaptureSink _sink;

        public CapturingHandler(CaptureSink sink) => _sink = sink;

        public Task HandleAsync(TEvent integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _sink.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private static (IEventBus Bus, CaptureSink Sink) BuildBus()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new EnergyPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<EnergyConsumptionRecorded>, CapturingHandler<EnergyConsumptionRecorded>>();
        services.AddScoped<IEventHandler<EnergySpikeDetected>, CapturingHandler<EnergySpikeDetected>>();

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IEventBus>(), sink);
    }

    private static MeterReadingReceived Reading(decimal value) => new()
    {
        Reading = new MeterReading
        {
            Tenant = "acme",
            MeterId = "main-incomer",
            Metric = "ActivePower",
            Value = value,
            Unit = "kWh",
            ReadingAt = DateTimeOffset.UnixEpoch,
        },
    };

    [Fact]
    public async Task A_reading_on_the_bus_drives_consumption_and_spike_events_back_onto_the_bus()
    {
        var (bus, sink) = BuildBus();

        for (var i = 0; i < 4; i++)
        {
            await bus.PublishAsync(Reading(100m));
        }

        await bus.PublishAsync(Reading(250m)); // +150% over the 100 baseline

        Assert.Equal(5, sink.Events.OfType<EnergyConsumptionRecorded>().Count());
        var spike = Assert.Single(sink.Events.OfType<EnergySpikeDetected>());
        Assert.Equal("acme", spike.Tenant);
        Assert.Equal(100m, spike.Baseline);
        Assert.Equal(150m, spike.DeltaPercent);
    }
}
