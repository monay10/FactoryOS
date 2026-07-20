using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Energy;
using FactoryOS.Plugins.Maintenance;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The cross-module showcase: the Energy and Maintenance plugins compose purely through the event bus, with no
/// reference between them. A stream of readings drives Energy to detect a spike, which drives Maintenance to
/// raise a work order — all as shared events on the one bus. This is Law 4 (event-driven only) end to end.
/// </summary>
public sealed class EnergyToMaintenanceTests
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
    public async Task An_energy_spike_raises_a_maintenance_work_order_over_the_bus()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new EnergyPlugin().ConfigureServices(services);       // consumes MeterReadingReceived, emits EnergySpikeDetected
        new MaintenancePlugin().ConfigureServices(services);  // consumes EnergySpikeDetected, emits WorkOrderCreated

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<WorkOrderCreated>, CapturingHandler<WorkOrderCreated>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        for (var i = 0; i < 4; i++)
        {
            await bus.PublishAsync(Reading(100m));
        }

        await bus.PublishAsync(Reading(250m)); // +150% over the 100 baseline → spike → work order

        var created = Assert.Single(sink.Events.OfType<WorkOrderCreated>());
        Assert.Equal("EnergySpike", created.Reason);
        Assert.Equal("acme", created.WorkOrder.Tenant);
        Assert.Equal("main-incomer", created.WorkOrder.AssetCode);
        Assert.Equal("Open", created.WorkOrder.Status);
    }
}
