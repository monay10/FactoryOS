using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.RuleEngine;
using FactoryOS.Plugins.RuleEngine.Application;
using FactoryOS.Plugins.RuleEngine.Domain;

namespace FactoryOS.Tests.RuleEngine;

public sealed class CarbonEmissionCalculatedHandlerTests
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

    private sealed record Harness(CarbonEmissionCalculatedHandler Handler, RecordingEventBus Bus);

    private static Harness Build(params RuleDefinition[] rules)
    {
        var bus = new RecordingEventBus();
        var options = new RuleEngineOptions { Rules = rules };
        return new Harness(new CarbonEmissionCalculatedHandler(bus, options, new InMemoryRuleFiringLog()), bus);
    }

    private static CarbonEmissionCalculated Emission(decimal co2eKg, Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        Source = "meter-7",
        Metric = "ActivePower",
        EnergyValue = 200m,
        EnergyUnit = "kWh",
        EmissionFactor = 0.4m,
        Co2eKg = co2eKg,
        CumulativeCo2eKg = co2eKg,
        OccurredAt = DateTimeOffset.UnixEpoch.AddHours(2),
        SourceEventId = Guid.NewGuid(),
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static readonly RuleDefinition CarbonSpike = new()
    {
        Id = "carbon-spike",
        Metric = "CarbonCo2e",
        Operator = ComparisonOperator.GreaterThan,
        Threshold = 50m,
        Action = "RaiseSustainabilityAlert",
    };

    [Fact]
    public async Task A_high_emission_triggers_the_rule_carrying_the_source_as_the_meter()
    {
        var h = Build(CarbonSpike);
        var evt = Emission(72m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var triggered = Assert.Single(h.Bus.Published.OfType<RuleTriggered>());
        Assert.Equal("carbon-spike", triggered.RuleId);
        Assert.Equal("acme", triggered.Tenant);
        Assert.Equal("CarbonCo2e", triggered.Metric);
        Assert.Equal("meter-7", triggered.MeterId); // the emitting source is carried as the meter
        Assert.Equal(72m, triggered.Value);
        Assert.Equal("GreaterThan", triggered.Operator);
        Assert.Equal(50m, triggered.Threshold);
        Assert.Equal("RaiseSustainabilityAlert", triggered.Action);
        Assert.Equal(evt.OccurredAt, triggered.TriggeredAt);
        Assert.Equal(evt.EventId, triggered.SourceEventId);
    }

    [Fact]
    public async Task A_low_emission_triggers_nothing()
    {
        var h = Build(CarbonSpike);
        var evt = Emission(12m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task A_non_carbon_rule_never_fires_on_emission_events()
    {
        var powerSpike = new RuleDefinition
        {
            Id = "power-spike",
            Metric = "ActivePower",
            Operator = ComparisonOperator.GreaterOrEqual,
            Threshold = 1m,
            Action = "NotifyEnergyDesk",
        };
        var h = Build(powerSpike);
        var evt = Emission(999m); // would match any low threshold, but the rule watches ActivePower

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Metric_matching_is_case_insensitive()
    {
        var h = Build(CarbonSpike with { Metric = "carbonco2e" });
        var evt = Emission(72m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Single(h.Bus.Published.OfType<RuleTriggered>());
    }

    [Fact]
    public async Task Redelivery_of_the_same_emission_event_fires_each_rule_once()
    {
        var h = Build(CarbonSpike);
        var evt = Emission(72m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<RuleTriggered>());
    }
}
