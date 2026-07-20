using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.RuleEngine;
using FactoryOS.Plugins.RuleEngine.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Rule Engine proven over the real bus: a Standard Model reading (<see cref="MeterReadingReceived"/>) that
/// crosses a configured threshold drives the module to emit <see cref="RuleTriggered"/>, and a redelivery of the
/// same reading emits nothing more. The rule is data, the producer of the reading and the consumer of the trigger
/// never reference each other, and the whole observation-to-action turn travels the bus.
/// </summary>
public sealed class RuleEngineTriggerTests
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

    private static MeterReadingReceived Reading(decimal value, Guid id) => new()
    {
        EventId = id,
        Reading = new MeterReading
        {
            Tenant = "acme",
            MeterId = "press-1",
            Metric = "Temperature",
            Value = value,
            Unit = "°C",
            ReadingAt = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
        },
    };

    private static OeeCalculated Oee(decimal oee, Guid id) => new()
    {
        EventId = id,
        Tenant = "acme",
        MachineId = "press-1",
        PeriodStart = new DateTimeOffset(2026, 7, 20, 5, 0, 0, TimeSpan.Zero),
        PeriodEnd = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
        Availability = 0.9m,
        Performance = 0.9m,
        Quality = 0.9m,
        Oee = oee,
        MeetsTarget = oee >= 0.8m,
    };

    private static CarbonEmissionCalculated Emission(decimal co2eKg, Guid id) => new()
    {
        EventId = id,
        Tenant = "acme",
        Source = "meter-7",
        Metric = "ActivePower",
        EnergyValue = 200m,
        EnergyUnit = "kWh",
        EmissionFactor = 0.4m,
        Co2eKg = co2eKg,
        CumulativeCo2eKg = co2eKg,
        OccurredAt = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public async Task A_reading_that_crosses_a_threshold_triggers_the_rule_once()
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

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<RuleTriggered>, CapturingHandler<RuleTriggered>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        var readingId = Guid.NewGuid();
        await bus.PublishAsync(Reading(90m, readingId));
        await bus.PublishAsync(Reading(90m, readingId)); // redelivery, same event id
        await bus.PublishAsync(Reading(80m, Guid.NewGuid())); // below threshold

        var triggered = Assert.Single(sink.Events.OfType<RuleTriggered>());
        Assert.Equal("overtemp-press-1", triggered.RuleId);
        Assert.Equal("RaiseMaintenanceAlert", triggered.Action);
        Assert.Equal(90m, triggered.Value);
        Assert.Equal(readingId, triggered.SourceEventId);
    }

    [Fact]
    public async Task Degraded_oee_triggers_the_rule_over_the_bus_once()
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
                    Id = "oee-degraded",
                    Metric = "Oee",
                    Operator = ComparisonOperator.LessThan,
                    Threshold = 0.6m,
                    Action = "RaiseMaintenanceAlert",
                },
            ],
        });
        new RuleEnginePlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<RuleTriggered>, CapturingHandler<RuleTriggered>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        var oeeId = Guid.NewGuid();
        await bus.PublishAsync(Oee(0.55m, oeeId));
        await bus.PublishAsync(Oee(0.55m, oeeId)); // redelivery, same event id
        await bus.PublishAsync(Oee(0.85m, Guid.NewGuid())); // healthy, above threshold

        var triggered = Assert.Single(sink.Events.OfType<RuleTriggered>());
        Assert.Equal("oee-degraded", triggered.RuleId);
        Assert.Equal("Oee", triggered.Metric);
        Assert.Equal("press-1", triggered.MeterId);
        Assert.Equal(0.55m, triggered.Value);
        Assert.Equal(oeeId, triggered.SourceEventId);
    }

    [Fact]
    public async Task A_carbon_spike_triggers_the_rule_over_the_bus_once()
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
                    Id = "carbon-spike",
                    Metric = "CarbonCo2e",
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 50m,
                    Action = "RaiseSustainabilityAlert",
                },
            ],
        });
        new RuleEnginePlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<RuleTriggered>, CapturingHandler<RuleTriggered>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        var emissionId = Guid.NewGuid();
        await bus.PublishAsync(Emission(72m, emissionId));
        await bus.PublishAsync(Emission(72m, emissionId)); // redelivery, same event id
        await bus.PublishAsync(Emission(12m, Guid.NewGuid())); // below threshold

        var triggered = Assert.Single(sink.Events.OfType<RuleTriggered>());
        Assert.Equal("carbon-spike", triggered.RuleId);
        Assert.Equal("CarbonCo2e", triggered.Metric);
        Assert.Equal("meter-7", triggered.MeterId);
        Assert.Equal(72m, triggered.Value);
        Assert.Equal("RaiseSustainabilityAlert", triggered.Action);
        Assert.Equal(emissionId, triggered.SourceEventId);
    }
}
