using System.Collections.Generic;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Carbon;
using FactoryOS.Plugins.Carbon.Application;
using FactoryOS.Plugins.Carbon.Domain;

namespace FactoryOS.Tests.Carbon;

public sealed class EnergyConsumptionRecordedHandlerTests
{
    private sealed class RecordingEventBus : IEventBus
    {
        public List<IIntegrationEvent> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent integrationEvent, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed record Harness(EnergyConsumptionRecordedHandler Handler, RecordingEventBus Bus, ICarbonLedger Ledger);

    private static Harness Build(decimal factor = 0.4m, decimal defaultFactor = 0m)
    {
        var bus = new RecordingEventBus();
        var ledger = new InMemoryCarbonLedger();
        var options = new CarbonOptions
        {
            EmissionFactors = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["ActivePower"] = factor },
            DefaultEmissionFactor = defaultFactor,
        };
        return new Harness(new EnergyConsumptionRecordedHandler(bus, ledger, new InMemoryProcessedEventLog(), options), bus, ledger);
    }

    private static EnergyConsumptionRecorded Consumption(string metric = "ActivePower", decimal value = 125m) => new()
    {
        Tenant = "acme",
        MeterId = "meter-1",
        Metric = metric,
        Value = value,
        Unit = "kWh",
        ReadingAt = DateTimeOffset.UnixEpoch,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task Computes_and_publishes_an_emission()
    {
        var h = Build();
        var consumption = Consumption(value: 125m);

        await h.Handler.HandleAsync(consumption, Context(consumption), CancellationToken.None);

        var calc = Assert.Single(h.Bus.Published.OfType<CarbonEmissionCalculated>());
        Assert.Equal("meter-1", calc.Source);
        Assert.Equal(0.4m, calc.EmissionFactor);
        Assert.Equal(50m, calc.Co2eKg);          // 125 × 0.4
        Assert.Equal(50m, calc.CumulativeCo2eKg);
        Assert.Equal(consumption.EventId, calc.SourceEventId);
    }

    [Fact]
    public async Task Cumulative_total_grows_across_readings()
    {
        var h = Build();
        await h.Handler.HandleAsync(Consumption(value: 100m), Context(Consumption(value: 100m)), CancellationToken.None);
        await h.Handler.HandleAsync(Consumption(value: 50m), Context(Consumption(value: 50m)), CancellationToken.None);

        Assert.Equal(60m, h.Bus.Published.OfType<CarbonEmissionCalculated>().Last().CumulativeCo2eKg); // 40 + 20
    }

    [Fact]
    public async Task A_metric_without_a_positive_factor_is_ignored()
    {
        var h = Build(); // default 0
        var consumption = Consumption(metric: "Unmapped");

        await h.Handler.HandleAsync(consumption, Context(consumption), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_of_the_same_reading_is_not_double_counted()
    {
        var h = Build();
        var consumption = Consumption(value: 125m);

        await h.Handler.HandleAsync(consumption, Context(consumption), CancellationToken.None);
        await h.Handler.HandleAsync(consumption, Context(consumption), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<CarbonEmissionCalculated>());
        Assert.Equal(50m, Assert.Single(h.Ledger.ForTenant("acme")).CumulativeCo2eKg); // not 100
    }
}
