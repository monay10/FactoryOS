using System.Collections.Concurrent;
using System.Collections.Generic;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Carbon;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// Proves the Carbon module works event-driven through the real in-process event bus: an
/// <see cref="EnergyConsumptionRecorded"/> published on the bus is consumed by the plugin's handler, which
/// converts it to a <see cref="CarbonEmissionCalculated"/> using the configured emission factor — the Energy →
/// Carbon chain, with neither module referencing the other.
/// </summary>
public sealed class EnergyToCarbonChainTests
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

    [Fact]
    public async Task An_energy_consumption_on_the_bus_yields_a_carbon_emission()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton(new CarbonOptions
        {
            EmissionFactors = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["ActivePower"] = 0.4m },
        });
        new CarbonPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<CarbonEmissionCalculated>, CapturingHandler<CarbonEmissionCalculated>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new EnergyConsumptionRecorded
        {
            Tenant = "acme",
            MeterId = "meter-1",
            Metric = "ActivePower",
            Value = 125m,
            Unit = "kWh",
            ReadingAt = DateTimeOffset.UnixEpoch,
        });

        var calc = Assert.Single(sink.Events.OfType<CarbonEmissionCalculated>());
        Assert.Equal("meter-1", calc.Source);
        Assert.Equal(50m, calc.Co2eKg); // 125 × 0.4
    }
}
