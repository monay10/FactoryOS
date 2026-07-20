using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.RuleEngine;
using FactoryOS.Plugins.RuleEngine.Application;
using FactoryOS.Plugins.RuleEngine.Domain;

namespace FactoryOS.Tests.RuleEngine;

public sealed class OeeCalculatedHandlerTests
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

    private sealed record Harness(OeeCalculatedHandler Handler, RecordingEventBus Bus);

    private static Harness Build(params RuleDefinition[] rules)
    {
        var bus = new RecordingEventBus();
        var options = new RuleEngineOptions { Rules = rules };
        return new Harness(new OeeCalculatedHandler(bus, options, new InMemoryRuleFiringLog()), bus);
    }

    private static OeeCalculated Oee(decimal oee, Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        MachineId = "press-1",
        PeriodStart = DateTimeOffset.UnixEpoch,
        PeriodEnd = DateTimeOffset.UnixEpoch.AddHours(1),
        Availability = 0.9m,
        Performance = 0.9m,
        Quality = 0.9m,
        Oee = oee,
        MeetsTarget = oee >= 0.8m,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static readonly RuleDefinition OeeDegraded = new()
    {
        Id = "oee-degraded",
        Metric = "Oee",
        Operator = ComparisonOperator.LessThan,
        Threshold = 0.6m,
        Action = "RaiseMaintenanceAlert",
    };

    [Fact]
    public async Task Degraded_oee_triggers_the_rule_carrying_the_machine_as_the_meter()
    {
        var h = Build(OeeDegraded);
        var evt = Oee(0.55m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var triggered = Assert.Single(h.Bus.Published.OfType<RuleTriggered>());
        Assert.Equal("oee-degraded", triggered.RuleId);
        Assert.Equal("acme", triggered.Tenant);
        Assert.Equal("Oee", triggered.Metric);
        Assert.Equal("press-1", triggered.MeterId); // the machine is carried as the meter
        Assert.Equal(0.55m, triggered.Value);
        Assert.Equal("LessThan", triggered.Operator);
        Assert.Equal(0.6m, triggered.Threshold);
        Assert.Equal("RaiseMaintenanceAlert", triggered.Action);
        Assert.Equal(evt.PeriodEnd, triggered.TriggeredAt);
        Assert.Equal(evt.EventId, triggered.SourceEventId);
    }

    [Fact]
    public async Task Healthy_oee_triggers_nothing()
    {
        var h = Build(OeeDegraded);
        var evt = Oee(0.85m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task A_non_oee_rule_never_fires_on_oee_events()
    {
        var overTemp = new RuleDefinition
        {
            Id = "overtemp-press-1",
            Metric = "Temperature",
            Operator = ComparisonOperator.GreaterThan,
            Threshold = 85m,
            Action = "RaiseMaintenanceAlert",
        };
        var h = Build(overTemp);
        var evt = Oee(0.10m); // would match any low threshold, but the rule watches Temperature

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Metric_matching_is_case_insensitive()
    {
        var h = Build(OeeDegraded with { Metric = "oee" });
        var evt = Oee(0.55m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Single(h.Bus.Published.OfType<RuleTriggered>());
    }

    [Fact]
    public async Task Redelivery_of_the_same_oee_event_fires_each_rule_once()
    {
        var h = Build(OeeDegraded);
        var evt = Oee(0.55m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<RuleTriggered>());
    }
}
